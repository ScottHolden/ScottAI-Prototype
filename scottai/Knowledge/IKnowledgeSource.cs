namespace ScottAIPrototype;

// TODO: Finish refactor of Azure Knowledge source over to here
// This branch currently doesn't implement the Azure Docs knowledge source, only the Azure Docs Link skill
internal interface IKnowledgeSource
{
	Task<string> QueryAsync(string input);
}
