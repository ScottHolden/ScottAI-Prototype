using Azure.Communication.Calling.WindowsClient;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using VoiceChat;
using WinRT;

namespace ScottAIPrototype;
public record TeamsMeeting(string TeamsMeetingLink);
public class ScottAI(
    TeamsMeeting meeting,
    VirtualMic virtualMic,
    AzureSpeech azureSpeech,
    FeatureFlags flags,
    IAIBackend aiBackend,
    IVideoRenderer? videoRenderer,
    IKnowledgeSource[]? knowledgeSources,
    VoiceChatACSConfig acsConfig,
    ILogger<ScottAI> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
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

        // Cache some common responses
        logger.LogInformation("Building common speech library...");
        var speechLibrary = await CachedSpeechLibrary.BuildAsync("speechCache", azureSpeech.SpeechSynthesizer, logger);

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

        List<ChatMessage> messages = [
            new ChatMessage(ChatMessageRole.System, personality.Prompt)
        ];

        // Configure Azure Open AI, starting prompt, and warm up
        await aiBackend.WarmUpAsync(CancellationToken.None);
        logger.LogInformation("OpenAI SDK Ready");

        // Set up our ACS Call and Chat clients
        var teamsCall = await ACSTeamsCall.CreateAgentAsync(acsConfig, personality.Name, meeting.TeamsMeetingLink);
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

        const int maxRagDataLength = 4000;
        List<(DateTimeOffset Time, string Data)> ragData = [];

        // Main processing loop
        // TODO: Refactor to orchestrator, this is messy
        async Task Process(CancellationToken parentCancel)
        {
            Task? currentOutstanding = null;
            CancellationTokenSource cancel = new();
            DateTime? lastSpoke = null;
            int hmmCount = 0;
            int nextHmm = 4;
            Task? lastRagTask = null;
            while (!parentCancel.IsCancellationRequested)
            {
                bool newMessages = false;
                while (recognizedQueue.TryDequeue(out string? message))
                {
                    if (message != null)
                    {
                        messages.Add(new ChatMessage(ChatMessageRole.User, message));
                        newMessages = true;
                        // Implement knowledge lookup for new content
                        if (knowledgeSources != null)
                        {
                            var messageTime = DateTimeOffset.UtcNow;
                            foreach (var source in knowledgeSources)
                            {
                                lastRagTask = source.QueryAsync(message).ContinueWith(x => ragData.Add((messageTime, x.Result)), parentCancel);
                            }

                        }
                    }
                }
                if (newMessages)
                {
                    if (currentOutstanding != null && !currentOutstanding.IsCompleted)
                    {
                        cancel.Cancel();
                        cancel = new CancellationTokenSource();
                    }
                    if (lastRagTask != null)
                    {
                        await lastRagTask;
                    }
                    var ragDataStringBuilder = new StringBuilder();
                    if (ragData.Count > 0)
                    {
                        // Could clean this up a bit
                        List<(DateTimeOffset Time, string Data)> newRagData = [];
                        int dataLength = 0;
                        foreach (var data in ragData.OrderByDescending(x => x.Time))
                        {
                            newRagData.Add(data);
                            if (dataLength > 0)
                            {
                                ragDataStringBuilder.Append("\n\n---\n\n");
                            }
                            if (data.Data.Length + dataLength > maxRagDataLength)
                            {
                                int limit = maxRagDataLength - dataLength;
                                ragDataStringBuilder.Append(data.Data[..limit]);
                                break;
                            }
                            ragDataStringBuilder.Append(data.Data);
                        }
                        // Swap our trimmed list back in
                        ragData = newRagData;
                    }
                    lastSpoke = DateTime.Now;
                    currentOutstanding = aiBackend.GetChatCompletionAsync(messages, ragDataStringBuilder.ToString(), cancel.Token)
                            .ContinueWith(x =>
                            {
                                logger.LogInformation("Response ({status}): {responseText}", x?.Status, x?.Result.Content);
                                lastSpoke = null;
                                hmmCount = 0;
                                var responseText = x?.Result.Content;
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
                                messages.Add(new ChatMessage(ChatMessageRole.Assistant, x.Result.Content));
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

        // Set up our virtual mic pipeline
        logger.LogInformation("Setting up outbound audio...");
        await virtualMic.StartAsync(teamsCall.RawOutgoingAudioStream);

        // Set up our visuals
        VirtualCam? vc = null;
        if (flags.NoVideo)
        {
            logger.LogInformation("Skipping VideoRenderer as No Video requested");
        }
        else
        {
            try
            {
                logger.LogInformation("Setting up OpenGL...");
                if (videoRenderer != null)
                {
                    vc = new VirtualCam(videoRenderer, logger);
                    await vc.StartAsync(teamsCall.RawOutgoingVideoStream, teamsCall.RenderSize);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error starting VideoRenderer/VirtualCam: {exceptionMessage}", ex.Message);
                logger.LogWarning("Disabling VideoRenderer, switching to No Video");
                vc = null;
                //videoRenderer?.Dispose();
                videoRenderer = null;
                flags = flags with
                {
                    NoVideo = true
                };
            }
            if (videoRenderer != null)
            {
                virtualMic.StartSpeaking += (o, e) => videoRenderer.SetAs(true);
                virtualMic.StopSpeaking += (o, e) => videoRenderer.SetAs(false);
                virtualMic.SpeechAmplitude += (o, e) => videoRenderer.SetAmp(e);
            }
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
                    videoRenderer?.FadeIn();
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
            videoRenderer?.FadeOut();
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
