using ScottAIPrototype;
using ScottAIPrototype.AI.AzureOpenAI;
using VoiceChat;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// We can control certain features of ScottAI via flags
builder.Services.AddSingleton<FeatureFlags>(FeatureFlags.Default with
{
    // AgentNameOverride = "NotScottAI"
});
builder.Services.AddSingleton<TeamsMeeting>(services =>
{
    var meetingLink = File.ReadAllText("meeting.txt").Trim();
    if (string.IsNullOrWhiteSpace(meetingLink))
    {
        throw new Exception("Teams meeting link is required in meeting.txt");
    }
    return new TeamsMeeting(meetingLink);
});

builder.Services.BindConfiguration<VoiceChatSpeechConfig>("Speech");
builder.Services.BindConfiguration<VoiceChatACSConfig>("ACS");
builder.Services.BindConfiguration<AzureOpenAIBackendConfig>("OpenAI");
builder.Services.BindConfiguration<AzureAISearchKnowledgeSourceConfig>("AISearch");

builder.Services.AddSingleton<VirtualMic>();
builder.Services.AddSingleton<AzureSpeech>();
builder.Services.AddSingleton<ScottAI>();
builder.Services.AddSingleton<IAIBackend, AzureOpenAIBackend>();
builder.Services.AddSingleton<MetadataKnowledgeSource>();
builder.Services.AddSingleton<AzureAISearchKnowledgeSource>();

builder.Services.AddSingleton<IKnowledgeSource[]>(services =>
    [
        services.GetRequiredService<MetadataKnowledgeSource>(),
        services.GetRequiredService<AzureAISearchKnowledgeSource>()
    ]
);


// Use fixed image
builder.Services.AddSingleton(new FixedImageVideoRendererConfig("robot-face.jpg"));
builder.Services.AddSingleton<IVideoRenderer, FixedImageVideoRenderer>();
// Option for OpenGL
//builder.Services.AddSingleton<IVideoRenderer, OpenGLVideoRenderer>();

builder.Services.AddHostedService<ScottAIService>();

// Build and run!
builder.Build().Run();
