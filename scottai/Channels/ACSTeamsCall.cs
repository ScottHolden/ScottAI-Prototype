using Azure;
using Azure.Communication.Calling.WindowsClient;
using Azure.Communication.Chat;
using Azure.Communication.Identity;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using VoiceChat;

namespace ScottAIPrototype;

public sealed class ACSTeamsCall : IDisposable
{
    public string ChatThreadId => _threadClient.Id;

    // TODO: Refactor these so we don't need to access them
    public RawOutgoingAudioStream RawOutgoingAudioStream => _rawOutgoingAudioStream;
    public RawIncomingAudioStream RawIncomingAudioStream => _rawIncomingAudioStream;
    public VirtualOutgoingVideoStream RawOutgoingVideoStream => _rawOutgoingVideoStream;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Will be made dynamic in future")]
    public RenderSize RenderSize => s_renderSize;


    private readonly ChatThreadClient _threadClient;
    private readonly CallClient _callClient;
    private readonly CallAgent _callAgent;
    private readonly TeamsMeetingLinkLocator _meetingLocator;
    private readonly RawOutgoingAudioStream _rawOutgoingAudioStream;
    private readonly RawIncomingAudioStream _rawIncomingAudioStream;
    private readonly VirtualOutgoingVideoStream _rawOutgoingVideoStream;
    private readonly JoinCallOptions _joinCallOptions;

    // Hardcoded config
    private static readonly RenderSize s_renderSize = new(1280, 720);
    private static readonly VideoStreamFormat s_videoStreamFormat = new()
    {
        Resolution = VideoStreamResolution.P720,
        PixelFormat = VideoStreamPixelFormat.Rgba,
        FramesPerSecond = 30,
        Stride1 = 1280 * 4 //720->1280
    };
    private static readonly RawOutgoingAudioStreamProperties s_outgoingAudioProperties = new()
    {
        Format = AudioStreamFormat.Pcm16Bit,
        SampleRate = AudioStreamSampleRate.Hz_48000,
        ChannelMode = AudioStreamChannelMode.Mono,
        BufferDuration = AudioStreamBufferDuration.Ms20
    };
    private static readonly RawIncomingAudioStreamProperties s_incomingAudioProperties = new()
    {
        Format = AudioStreamFormat.Pcm16Bit,
        SampleRate = AudioStreamSampleRate.Hz_16000,
        ChannelMode = AudioStreamChannelMode.Mono
    };

    private ACSTeamsCall(CallClient callClient, CallAgent callAgent, ChatThreadClient threadClient, TeamsMeetingLinkLocator meetingLocator)
    {
        _callClient = callClient;
        _callAgent = callAgent;
        _threadClient = threadClient;
        _meetingLocator = meetingLocator;

        // Outgoing Audio
        var outgoingAudioStreamOptions = new RawOutgoingAudioStreamOptions()
        {
            Properties = s_outgoingAudioProperties
        };
        _rawOutgoingAudioStream = new RawOutgoingAudioStream(outgoingAudioStreamOptions);
        var outgoingAudioOptions = new OutgoingAudioOptions
        {
            IsMuted = false,
            Stream = _rawOutgoingAudioStream,
        };

        // Incoming Audio
        var incomingAudioStreamOptions = new RawIncomingAudioStreamOptions()
        {
            Properties = s_incomingAudioProperties
        };
        _rawIncomingAudioStream = new RawIncomingAudioStream(incomingAudioStreamOptions);
        var incomingAudioOptions = new IncomingAudioOptions()
        {
            IsMuted = false,
            Stream = _rawIncomingAudioStream
        };

        // Outgoing Video
        VideoStreamFormat[] videoStreamFormats = { s_videoStreamFormat };
        var rawOutgoingVideoStreamOptions = new RawOutgoingVideoStreamOptions
        {
            Formats = videoStreamFormats
        };
        _rawOutgoingVideoStream = new VirtualOutgoingVideoStream(rawOutgoingVideoStreamOptions);
        var outgoingVideoOptions = new OutgoingVideoOptions()
        {
            Streams = new List<OutgoingVideoStream> { _rawOutgoingVideoStream }
        };

        // Final call options
        _joinCallOptions = new JoinCallOptions
        {
            OutgoingAudioOptions = outgoingAudioOptions,
            IncomingAudioOptions = incomingAudioOptions,
            OutgoingVideoOptions = outgoingVideoOptions
        };
    }
    public async Task<CommunicationCall> JoinAsync()
        => await _callAgent.JoinAsync(_meetingLocator, _joinCallOptions);

    public Task SendHtmlMessageAsync(string content)
        => _threadClient.SendMessageAsync(new SendChatMessageOptions()
        {
            Content = content,
            MessageType = ChatMessageType.Html
        });

    public static async Task<ACSTeamsCall> CreateAgentAsync(VoiceChatACSConfig config, string displayName, string teamsMeetingLink)
    {
        var acsEndpoint = new Uri(config.Endpoint);
        var acsKey = new AzureKeyCredential(config.Key);
        var acsClient = new CommunicationIdentityClient(acsEndpoint, acsKey);
        var callClient = new CallClient();

        var identityAndTokenResponse = await acsClient.CreateUserAndTokenAsync(scopes: new[] { CommunicationTokenScope.VoIP, CommunicationTokenScope.Chat });

        var callCredential = new CallTokenCredential(identityAndTokenResponse.Value.AccessToken.Token);
        var callAgentOptions = new CallAgentOptions()
        {
            DisplayName = displayName
        };
        var callAgent = await callClient.CreateCallAgentAsync(callCredential, callAgentOptions);

        var chatCredential = new Azure.Communication.CommunicationTokenCredential(identityAndTokenResponse.Value.AccessToken.Token);
        var chatClient = new ChatClient(acsEndpoint, chatCredential);

        var meetingLocator = new TeamsMeetingLinkLocator(teamsMeetingLink);
        var threadId = TeamsMeetingHelper.ExtractThreadIdFromTeamsLink(meetingLocator.MeetingLink.ToString());
        var threadClient = chatClient.GetChatThreadClient(threadId);

        // We hand over the lifetime of the client and agent to the class
        return new ACSTeamsCall(
            callClient,
            callAgent,
            threadClient,
            meetingLocator
        );
    }

    public void ConfigureStreamDebugLogging(ILogger logger)
    {
        _rawOutgoingAudioStream.StateChanged += (o, e) => logger.LogInformation("rawOutgoingAudioStream: {audioOutStreamState}", e.Stream.State);
        _rawIncomingAudioStream.StateChanged += (o, e) => logger.LogInformation("rawIncomingAudioStream: {audioInStreamState}", e.Stream.State);
        _rawOutgoingVideoStream.StateChanged += (o, e) => logger.LogInformation("rawOutgoingVideoStream: {videoOutStreamState}", e.Stream.State);
        _rawOutgoingVideoStream.FormatChanged += (o, e) => logger.LogInformation("rawOutgoingVideoStream format: {videoResolution} {pixelFormat} {videoFPS}", e.Format.Resolution, e.Format.PixelFormat, e.Format.FramesPerSecond);

    }

    public void Dispose()
    {
        _callAgent.Dispose();
        _callClient.Dispose();
    }
}
