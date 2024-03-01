namespace ScottAIPrototype;

// TODO: Finish refactor of Azure Knowledge source over to here
// This branch currently doesn't implement the Azure Docs knowledge source, only the Azure Docs Link skill
public interface IKnowledgeSource
{
    string Name { get; }
    Task<string> QueryAsync(string input);
}
