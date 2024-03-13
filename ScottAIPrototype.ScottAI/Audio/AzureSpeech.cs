using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;

namespace ScottAIPrototype;

public class AzureSpeech : IDisposable
{
    public PushAudioInputStream AudioStream => _audioStream;
    public SpeechSynthesizer SpeechSynthesizer => _speechSynthesizer;
    public SpeechRecognizer SpeechRecognizer => _speechRecognizer;

    private readonly PushAudioInputStream _audioStream;
    private readonly AudioConfig _audioConfig;
    private readonly SpeechSynthesizer _speechSynthesizer;
    private readonly SpeechRecognizer _speechRecognizer;
    private readonly ILogger _logger;
    private static readonly string[] s_defaultLanguages = new[] { "en-US", "de-DE", "zh-CN", "ja-JP" };

    public AzureSpeech(VoiceChatSpeechConfig config, FeatureFlags flags, ILogger<AzureSpeech> logger)
    {
        var speechConfig = SpeechConfig.FromSubscription(config.Key, config.Region);
        speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw48Khz16BitMonoPcm);
        // This default voice will be overridden in SSML
        speechConfig.SpeechSynthesisVoiceName = "en-US-BrandonNeural";

        _speechSynthesizer = new SpeechSynthesizer(speechConfig, null);

        _audioStream = AudioInputStream.CreatePushStream();
        _audioConfig = AudioConfig.FromStreamInput(_audioStream);

        if (flags.MultiLanguageSupport)
        {
            var languageConfig = AutoDetectSourceLanguageConfig.FromLanguages(s_defaultLanguages);
            _speechRecognizer = new SpeechRecognizer(speechConfig, languageConfig, _audioConfig);
        }
        else
        {
            _speechRecognizer = new SpeechRecognizer(speechConfig, _audioConfig);
        }

        _logger = logger;


        if (flags.DebugLogging)
        {
            _speechRecognizer.Canceled += (o, e) => _logger.LogInformation($"speechRecognizer Canceled: {e.Reason} {e.ErrorCode} {e.ErrorDetails}");
            _speechRecognizer.SessionStarted += (o, e) => _logger.LogInformation($"speechRecognizer SessionStarted: {e.SessionId}");
            _speechRecognizer.SessionStopped += (o, e) => _logger.LogInformation($"speechRecognizer SessionStopped: {e.SessionId}");
            _speechRecognizer.SpeechStartDetected += (o, e) => _logger.LogInformation($"speechRecognizer SpeechStartDetected: {e.SessionId}");
            _speechRecognizer.SpeechEndDetected += (o, e) => _logger.LogInformation($"speechRecognizer SpeechEndDetected: {e.SessionId}");
        }
        // TODO: Shift recognized and recognizing here
    }

    public void Dispose()
    {
        _audioStream.Dispose();
        _audioConfig.Dispose();
        _speechSynthesizer.Dispose();
    }
    // TODO: Re-add multi-lang prompt switch
    /*
		var globalLang = "en-US";
		var globalVoice = "en-US-BrandonNeural";
		Action<string> setLang = x =>
		{
			if (!flags.MultiLanguageSupport) return;
			var lang = "en-US";
			if (x != "Unknown") lang = x;
			chatCompletionsOptions.Messages[0].Content = personality.Prompt + $"\nThe user only speaks {lang}, please reply in {lang}.\nTranslate all responses to {lang}";
			globalLang = lang;
			globalVoice = lang switch
			{
				"de-DE" => "de-DE-ConradNeural",
				"zh-CN" => "zh-CN-YunxiNeural",
				"ja-JP" => "ja-JP-KeitaNeural",
				"en-US" => "en-US-BrandonNeural",
				_ => "en-US-BrandonNeural"
			};
			logger.LogInformation("Updated prompt langauge to " + lang);
		};
		*/
}
