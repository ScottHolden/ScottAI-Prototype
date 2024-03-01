using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace ScottAIPrototype.AI.AzureOpenAI;

public class AzureOpenAIBackend(
    AzureOpenAIBackendConfig _config,
    ILogger<AzureOpenAIBackend> _logger
) : IAIBackend
{
    private readonly OpenAIClient _openAIClient = new(
        new Uri(_config.Endpoint),
        new Azure.AzureKeyCredential(_config.Key)
    );
    public async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string input)
    {
        var options = new EmbeddingsOptions(_config.EmbeddingDeployment, [input]);
        var result = await _openAIClient.GetEmbeddingsAsync(options);
        _logger.LogInformation("Embedding deployment usage '{deployment}' tokens: {tokens}", _config.EmbeddingDeployment, result.Value.Usage.PromptTokens);
        return result.Value.Data[0].Embedding;
    }
}