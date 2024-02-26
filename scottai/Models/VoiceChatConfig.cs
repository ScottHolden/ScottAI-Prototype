using System.Text.Json;

namespace ScottAIPrototype;

public record VoiceChatConfig(VoiceChatSpeechConfig Speech, VoiceChatACSConfig ACS, VoiceChatOpenAIConfig OpenAI)
{
	public static VoiceChatConfig Load(string path)
		=> JsonSerializer.Deserialize<VoiceChatConfig>(File.ReadAllText(path), new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			AllowTrailingCommas = true,
		}) ?? throw new Exception("Unable to load config");
}
public record VoiceChatSpeechConfig(string Key, string Region);
public record VoiceChatACSConfig(string Endpoint, string Key);
public record VoiceChatOpenAIConfig(string Endpoint, string Key, string Deployment);