using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using ScottAIPrototype;
using System.Text.Json;

internal class ScottAIConfig
{
    public static async Task<VoiceChatConfig> ConfigFromFileAsync(string filename = "config.json")
        => JsonSerializer.Deserialize<VoiceChatConfig>(await File.ReadAllTextAsync(filename), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
        }) ?? throw new Exception("Unable to load config");
    public static async Task<VoiceChatConfig> ConfigFromKeyVaultAsync(string keyVaultEndpoint, string tenantId = "")
    {
        var credOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(tenantId)) credOptions.TenantId = tenantId;
        var client = new SecretClient(new Uri(keyVaultEndpoint), new DefaultAzureCredential(credOptions));

        async Task<string> GetSecretAsync(string name)
            => (await client!.GetSecretAsync(name)).Value.Value;

        return new VoiceChatConfig(
            new VoiceChatSpeechConfig(
                await GetSecretAsync("SCOTTAI_SPEECH_KEY"),
                await GetSecretAsync("SCOTTAI_SPEECH_REGION")
            ),
            new VoiceChatACSConfig(
                await GetSecretAsync("SCOTTAI_ACS_ENDPOINT"),
                await GetSecretAsync("SCOTTAI_ACS_KEY")
            ),
            new VoiceChatOpenAIConfig(
                await GetSecretAsync("SCOTTAI_AOAI_ENDPOINT"),
                await GetSecretAsync("SCOTTAI_AOAI_KEY"),
                await GetSecretAsync("SCOTTAI_AOAI_DEPLOYMENT")
            )
        );
    }
}