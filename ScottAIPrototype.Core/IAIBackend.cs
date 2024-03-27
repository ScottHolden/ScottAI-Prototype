namespace ScottAIPrototype;

public interface IAIBackend
{
    Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string input, CancellationToken cancellationToken);
    Task<ChatMessage> GetChatCompletionAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken);
    Task WarmUpAsync(CancellationToken cancellationToken);
}

public static class IAIBackendExtensions
{
    public static Task<ChatMessage> GetChatCompletionAsync(this IAIBackend aiBackend, IEnumerable<ChatMessage> messages, IEnumerable<string> ragData, CancellationToken cancellationToken)
           => aiBackend.GetChatCompletionAsync(messages, string.Join("\n\n", ragData), cancellationToken);
    public static Task<ChatMessage> GetChatCompletionAsync(this IAIBackend aiBackend, IEnumerable<ChatMessage> messages, string ragData, CancellationToken cancellationToken)
        => messages.Count() >= 2 ?
            aiBackend.GetChatCompletionAsync(messages.SkipLast(1).Append(new ChatMessage(ChatMessageRole.RagResult, ragData)).Append(messages.Last()), cancellationToken) :
            aiBackend.GetChatCompletionAsync(messages.Append(new ChatMessage(ChatMessageRole.RagResult, ragData)), cancellationToken);
}

public record ChatMessage(ChatMessageRole Role, string Content);
public enum ChatMessageRole
{
    System,
    User,
    Assistant,
    RagResult
}