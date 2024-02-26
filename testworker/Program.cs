using Microsoft.Extensions.Logging;
using ScottAIPrototype;

// Read the meeting URL from a txt file (easy to update)
var meetingLink = File.ReadAllText("meeting.txt").Trim();
if (string.IsNullOrWhiteSpace(meetingLink)) throw new Exception("Teams meeting link is required in meeting.txt");

// You can load config from a json file
var config = await ScottAIConfig.ConfigFromFileAsync("config.json");

// We can control certain features of ScottAI via flags
var flags = FeatureFlags.Default with
{
    // AgentNameOverride = "NotScottAI"
};

// As this is running single instance locally, build a console logger
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ScottAI>();

// Run will automatically join the call, and will only return once asked to leave/kicked
await ScottAI.RunAsync(config, meetingLink, flags, logger);
