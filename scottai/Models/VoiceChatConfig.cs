namespace ScottAIPrototype;

public record VoiceChatSpeechConfig(string Key, string Region);
public record VoiceChatACSConfig(string Endpoint, string Key);
public record VoiceChatOpenAIConfig(string Endpoint, string Key, string Deployment);