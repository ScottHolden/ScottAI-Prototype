namespace ScottAIPrototype;

public class MetadataKnowledgeSource : IKnowledgeSource
{
    public string Name { get; } = "metadata";

    public Task<string> QueryAsync(string question)
        => Task.FromResult($"""
            Todays Date: {DateTime.Now}"
            """);
}
