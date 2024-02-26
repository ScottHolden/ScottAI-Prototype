using Azure;
using Azure.AI.OpenAI;
using Azure.Communication.Calling.WindowsClient;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using VoiceChat;
using WinRT;

namespace ScottAIPrototype;
public class ScottAI
{
    public static async Task RunAsync(VoiceChatConfig config, string teamsMeetingLink, FeatureFlags flags, ILogger logger)
    {
        logger.LogInformation("Setting up ScottAI...");

        // Feature flag checking
        // TODO: Implement all
        if (flags.MultiLanguageSupport)
        {
            logger.LogWarning("Multi Language Support not available in this build, disabling...");
            flags = flags with
            {
                MultiLanguageSupport = false
            };
        }

        // Set up our TTS and STT connections
        var azureSpeech = new AzureSpeech(config.Speech, flags);
        if (flags.DebugLogging)
        {
            azureSpeech.ConfigureSpeechDebugLogging(logger);
        }

        // Cache some common responses
        logger.LogInformation("Building common speech library...");
        var speechLibrary = await CachedSpeechLibrary.BuildAsync("speechCache", azureSpeech.SpeechSynthesizer, logger);

        // Build our virtual mic, but don't start it yet.
        var virtualMic = new VirtualMic();

        // Configure our signaling & interruption path
        TaskCompletionSource callOver = new();
        ConcurrentQueue<string> recognizedQueue = new();
        var isSpeaking = false;
        // TODO: Move to AzureSpeech
        azureSpeech.SpeechRecognizer.Recognizing += (o, e) =>
        {
            if (e.Result.Text.Trim().Length > 6)
            {
                virtualMic.Interrupt();
                isSpeaking = true;
            }
            logger.LogInformation("Recognizing: {partialRecognition}", e.Result.Text);
        };
        logger.LogInformation("Speech SDK Ready");

        // Build out skills and personality
        var skills = new ISkill[] {
            new SearchDoco()
        };
        var personality = new Personality(skills, flags);

        // Configure Azure Open AI, starting prompt, and warm up
        var openAIClient = new OpenAIClient(new Uri(config.OpenAI.Endpoint), new AzureKeyCredential(config.OpenAI.Key));
        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            DeploymentName = config.OpenAI.Deployment,
            Messages =
            {
                new ChatRequestSystemMessage(personality.Prompt),
            },
            MaxTokens = 80
        };
        Response<ChatCompletions> response = await openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
        logger.LogInformation("OpenAI SDK Ready");

        // Set up our ACS Call and Chat clients
        var teamsCall = await ACSTeamsCall.CreateAgentAsync(config.ACS, personality.Name, teamsMeetingLink);
        if (flags.DebugLogging)
        {
            teamsCall.ConfigureStreamDebugLogging(logger);
            logger.LogInformation("Teams Chat Thread ID: {teamsChatThreadId}", teamsCall.ChatThreadId);
        }

        // Set up our speech recognition
        // TODO: Shift to AzureSpeech
        azureSpeech.SpeechRecognizer.Recognized += (o, e) =>
        {
            isSpeaking = false;
            try
            {
                // TODO: Re-add language switching
                //var autoDetectSourceLanguageResult = AutoDetectSourceLanguageResult.FromResult(e.Result);
                //logger.LogInformation($"Recognized [{autoDetectSourceLanguageResult.Language}]: {e.Result.Text}");
                //setLang(autoDetectSourceLanguageResult.Language);

                logger.LogInformation("Recognized: {recognizedText}", e.Result.Text);
                if (string.IsNullOrWhiteSpace(e.Result.Text)) return;
                recognizedQueue.Enqueue(e.Result.Text);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error when speech recognized {recognizedError}", ex.Message);
            }
        };

        // Main processing loop
        // TODO: Refactor to orchestrator, this is messy
        async Task Process(CancellationToken parentCancel)
        {
            Task? currentOutstanding = null;
            CancellationTokenSource cancel = new();
            DateTime? lastSpoke = null;
            int hmmCount = 0;
            int nextHmm = 4;
            while (!parentCancel.IsCancellationRequested)
            {
                bool newMessages = false;
                while (recognizedQueue.TryDequeue(out string? message))
                {
                    if (message != null)
                    {
                        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(message));
                        newMessages = true;
                    }
                }
                if (newMessages)
                {
                    if (currentOutstanding != null && !currentOutstanding.IsCompleted)
                    {
                        cancel.Cancel();
                        cancel = new CancellationTokenSource();
                    }
                    lastSpoke = DateTime.Now;
                    currentOutstanding = openAIClient.GetChatCompletionsAsync(chatCompletionsOptions, cancel.Token)
                            .ContinueWith(x =>
                            {
                                logger.LogInformation("Response ({status}): {responseText}", x?.Status, x?.Result?.Value?.Choices[0].Message.Content);
                                lastSpoke = null;
                                hmmCount = 0;
                                var responseText = x?.Result.Value.Choices[0].Message.Content;
                                if (x == null || string.IsNullOrWhiteSpace(responseText)) return;
                                if (responseText.StartsWith("[LISTENING]", StringComparison.CurrentCultureIgnoreCase)) return;
                                if (responseText.StartsWith("[EXIT]", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    callOver.SetResult();
                                    return;
                                };
                                responseText = responseText.Replace("[LISTENING]", "");

                                foreach (var skill in skills)
                                {
                                    if (responseText.StartsWith(skill.Trigger, StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        var input = responseText.Replace(skill.Trigger, "", StringComparison.OrdinalIgnoreCase).Trim();
                                        skill.Invoke(input, (speak) =>
                                        {
                                            // TODO: Implement cache lookup
                                            virtualMic.SpeakNow(speechLibrary.SureDropItInChat.Data);
                                            return Task.CompletedTask;
                                        }, teamsCall.SendHtmlMessageAsync);

                                        return;
                                    }
                                }
                                chatCompletionsOptions.Messages.Add(new ChatRequestAssistantMessage(x.Result.Value.Choices[0].Message.Content));
                                if (!isSpeaking)
                                {

                                    var lang = personality.Language;
                                    var voice = personality.Voice;
                                    var ssml = $"<speak xmlns=\"http://www.w3.org/2001/10/synthesis\" xmlns:mstts=\"http://www.w3.org/2001/mstts\" xmlns:emo=\"http://www.w3.org/2009/10/emotionml\" version=\"1.0\" xml:lang=\"{lang}\"><voice name=\"{voice}\">{responseText}</voice></speak>";
                                    azureSpeech.SpeechSynthesizer.SpeakSsmlAsync(ssml).ContinueWith(y =>
                                    {
                                        try
                                        {
                                            virtualMic.SpeakNow(y.Result.AudioData);
                                        }
                                        finally
                                        {
                                            y.Result.Dispose();
                                        }
                                    });
                                }
                            }, cancel.Token);
                }
                if (lastSpoke != null && (DateTime.Now - lastSpoke.Value).TotalSeconds > (nextHmm))
                {
                    nextHmm = Random.Shared.Next(4, 7);
                    lastSpoke = DateTime.Now;
                    hmmCount++;
                    if (hmmCount <= 3)
                    {
                        logger.LogInformation("Hmm");
                        virtualMic.MaybeSpeak(speechLibrary.RandomFiller().Data);
                    }
                    else
                    {
                        logger.LogInformation("Giving up on this question, what should I do?!?");
                        lastSpoke = null;
                        hmmCount = 0;
                        cancel.Cancel();
                        virtualMic.SpeakNow(speechLibrary.GetBackToYou.Data);
                    }
                }
                try
                {
                    await Task.Delay(100, parentCancel);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            cancel?.Cancel();
            if (currentOutstanding != null) await currentOutstanding;
        }

        // Set up our receive path with skip signaling
        // TODO: Refactor to speech pipeline
        bool audioReceivedSkip = false;
        void AudioReceived(object? sender, IncomingMixedAudioEventArgs e)
        {
            if (audioReceivedSkip) return;
            try
            {
                byte[] buffer = MemoryBufferHelpers.GetArrayBuffer(e.AudioBuffer.Buffer);
                azureSpeech.AudioStream.Write(buffer);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AudioReceived exception");
            }
        }
        teamsCall.RawIncomingAudioStream.MixedAudioBufferReceived += AudioReceived;
        logger.LogInformation("ACS SDK Ready");

        // Set up our visuals
        // This is a quick OpenGL shader to render something nice to look at
        // We have feature flags to turn this off when not needed, or on machines with no GPU
        OpenGLVideoRenderer? openGl = null;
        VirtualCam? vc = null;
        if (flags.NoVideo)
        {
            logger.LogInformation("Skipping OpenGL as No Video requested");
        }
        else
        {
            try
            {
                logger.LogInformation("Setting up OpenGL...");
                openGl = new OpenGLVideoRenderer(teamsCall.RenderSize, logger);
                vc = new VirtualCam(openGl, logger);
                await vc.StartAsync(teamsCall.RawOutgoingVideoStream);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error starting OpenGL/VirtualCam: {exceptionMessage}", ex.Message);
                logger.LogWarning("Disabling OpenGL, switching to No Video");
                vc = null;
                openGl?.Dispose();
                openGl = null;
                flags = flags with
                {
                    NoVideo = true
                };
            }
        }

        // Set up our virtual mic pipeline
        logger.LogInformation("Setting up outbound audio...");
        await virtualMic.StartAsync(teamsCall.RawOutgoingAudioStream);
        // Only attach mic to openGL if needed
        if (openGl != null)
        {
            virtualMic.StartSpeaking += (o, e) => openGl.SetAs(true);
            virtualMic.StopSpeaking += (o, e) => openGl.SetAs(false);
            virtualMic.SpeechAmplitude += (o, e) => openGl.SetAmp(e);
        }

        // We are ready to go!
        try
        {
            logger.LogInformation("Dialing into Teams call...");
            using var call = await teamsCall.JoinAsync();
            bool hasGreeted = false;

            call.StateChanged += async (o, e) =>
            {
                logger.LogInformation("{callState} - {callEndReasonCode}.{callEndReasonSubcode}", call?.State, call?.CallEndReason?.Code, call?.CallEndReason?.Subcode);
                if (call?.State == CallState.Connected && !hasGreeted)
                {
                    await azureSpeech.SpeechRecognizer.StartContinuousRecognitionAsync();
                    hasGreeted = true;
                    await Task.Delay(1500);
                    openGl?.FadeIn();
                    await Task.WhenAll(
                        Task.Delay(500),
                        teamsCall.SendHtmlMessageAsync(personality.WelcomeTextMessage)
                    );
                    virtualMic.SpeakNow(speechLibrary.Greeting.Data);
                }
            };
            logger.LogInformation("Listening...");
            CancellationTokenSource cancel = new();
            var processTask = Process(cancel.Token);
            await Task.WhenAny(callOver.Task, processTask);
            cancel.Cancel();

            audioReceivedSkip = true;
            teamsCall.RawIncomingAudioStream.MixedAudioBufferReceived -= AudioReceived;
            await azureSpeech.SpeechRecognizer.StopContinuousRecognitionAsync();

            // Goodbye
            virtualMic.SpeakNow(speechLibrary.Goodbye.Data);
            await Task.Delay(500);
            openGl?.FadeOut();
            await Task.Delay(speechLibrary.Goodbye.AudioDuration);

            if (call.State == CallState.Connected || call.State == CallState.InLobby)
            {
                logger.LogInformation("Leaving...");
                await call.HangUpAsync(new HangUpOptions());
            }

            await virtualMic.StopAsync();
            if (vc != null) await vc.StopAsync();
            await processTask;
        }
        catch (Exception e)
        {
            logger.LogError(e, "On-Call Exception");
        }
        finally
        {
            // May not be needed.
            audioReceivedSkip = true;
            teamsCall.RawIncomingAudioStream.MixedAudioBufferReceived -= AudioReceived;
        }
        logger.LogInformation("EOF");
    }
}
