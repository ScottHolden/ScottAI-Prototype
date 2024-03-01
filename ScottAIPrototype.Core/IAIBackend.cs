namespace ScottAIPrototype;

public interface IAIBackend
{
    Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string input);
}