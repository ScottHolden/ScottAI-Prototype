namespace ScottAIPrototype;

public record FeatureFlags(
string? AgentNameOverride,
bool MultiLanguageSupport,
bool NoVideo,
bool DebugLogging
)
{
	public static FeatureFlags Default => new(null, false, false, true);
}