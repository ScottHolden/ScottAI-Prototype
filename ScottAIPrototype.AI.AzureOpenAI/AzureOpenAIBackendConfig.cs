namespace ScottAIPrototype.AI.AzureOpenAI;

public record AzureOpenAIBackendConfig(
    string Endpoint,
    string Key,
    string ChatDeployment,
    string EmbeddingDeployment
);