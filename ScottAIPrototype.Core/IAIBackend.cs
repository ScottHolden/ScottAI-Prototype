namespace ScottAIPrototype;

public interface IAIBackend
{
    Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string input, CancellationToken cancellationToken);
    Task<ChatMessage> GetChatCompletionAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken);
    Task WarmUpAsync(CancellationToken cancellationToken);
}

public record ChatMessage(ChatMessageRole Role, string Content);
public enum ChatMessageRole
{
    System,
    User,
    Assistant
}