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

    public async Task<ChatMessage> GetChatCompletionAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        var options = new ChatCompletionsOptions(_config.ChatDeployment, ToChatRequestMessages(messages));
        var result = await _openAIClient.GetChatCompletionsAsync(options, cancellationToken);
        _logger.LogInformation("Chat deployment usage '{deployment}', prompt tokens: {promptTokens}, completion tokens: {completionTokens}", _config.ChatDeployment, result.Value.Usage.PromptTokens, result.Value.Usage.CompletionTokens);
        return ToChatMessage(result.Value.Choices[0].Message);
    }
    private static ChatMessage ToChatMessage(ChatResponseMessage message)
        => new(message.Role.ToString() switch
        {
            "user" => ChatMessageRole.User,
            "assistant" => ChatMessageRole.Assistant,
            _ => throw new NotImplementedException(),
        }, message.Content);
    private static IEnumerable<ChatRequestMessage> ToChatRequestMessages(IEnumerable<ChatMessage> messages)
        => messages.Select(x => (ChatRequestMessage)(x.Role switch
        {
            ChatMessageRole.System => new ChatRequestSystemMessage(x.Content),
            ChatMessageRole.User => new ChatRequestUserMessage(x.Content),
            ChatMessageRole.Assistant => new ChatRequestAssistantMessage(x.Content),
            ChatMessageRole.RagResult => new ChatRequestFunctionMessage("rag", x.Content),
            _ => throw new NotImplementedException(),
        }));
    public async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string input, CancellationToken cancellationToken)
    {
        var options = new EmbeddingsOptions(_config.EmbeddingDeployment, [input]);
        var result = await _openAIClient.GetEmbeddingsAsync(options, cancellationToken);
        _logger.LogInformation("Embedding deployment usage '{deployment}' tokens: {tokens}", _config.EmbeddingDeployment, result.Value.Usage.PromptTokens);
        return result.Value.Data[0].Embedding;
    }

    public Task WarmUpAsync(CancellationToken cancellationToken)
        => GetChatCompletionAsync([
            new ChatMessage(ChatMessageRole.System, "Respond with warm"),
            new ChatMessage(ChatMessageRole.User, "warm?")
        ], cancellationToken);
}