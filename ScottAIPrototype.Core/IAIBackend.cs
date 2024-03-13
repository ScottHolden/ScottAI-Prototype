namespace ScottAIPrototype;

public interface IAIBackend
{
    Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string input);
    Task<ChatMessage> GetChatCompletion(IEnumerable<ChatMessage> messages);
}

public record ChatMessage(ChatMessageRole Role, string Content);
public enum ChatMessageRole
{
    System,
    User,
    Assistant
}