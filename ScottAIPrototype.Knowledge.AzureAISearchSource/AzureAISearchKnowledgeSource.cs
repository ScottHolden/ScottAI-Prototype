
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using System.Text;

namespace ScottAIPrototype;

public record AzureAISearchKnowledgeSourceConfig(string Endpoint, string Key, string Index);

public class AzureAISearchKnowledgeSource(AzureAISearchKnowledgeSourceConfig _config) : IKnowledgeSource
{
    public string Name => "aisearch";
    private readonly SearchClient _searchClient = new(
        new Uri(_config.Endpoint),
        _config.Index,
        new AzureKeyCredential(_config.Key)
    );
    private readonly string _semanticSearchName = "default";
    private readonly string _embeddingFieldName = "embedding";
    private readonly int _limit = 5;
    public async Task<string> QueryAsync(string input)
    {
        // TODO: Add embedding generation
        float[]? vector = null;

        // TODO: Add highlight based responses
        var searchOptions = new SearchOptions()
        {
            QueryType = SearchQueryType.Semantic,
            VectorSearch = new VectorSearchOptions
            {
                Queries = {
                    new VectorizedQuery(vector)
                    {
                        KNearestNeighborsCount = _limit,
                        Fields = {
                            nameof(_embeddingFieldName)
                        },
                    }
            }
            },
            SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = _semanticSearchName,
                QueryCaption = new QueryCaption(QueryCaptionType.Extractive)
                {
                    HighlightEnabled = false
                },
            },
        };

        var result = await _searchClient.SearchAsync<IndexFields>(input, searchOptions);

        StringBuilder output = new();
        int count = 0;
        await foreach (var item in result.Value.GetResultsAsync())
        {
            output.AppendLine(item.Document.content);
            if (++count >= _limit) break;
        }

        return output.ToString();
    }
    private record IndexFields(string content);
}
