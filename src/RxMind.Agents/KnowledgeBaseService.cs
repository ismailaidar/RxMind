using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace RxMind.Agents;

public class KnowledgeBaseService
{
    private readonly SearchClient _searchClient;

    public KnowledgeBaseService()
    {
        _searchClient = new SearchClient(
            new Uri(Config.SearchEndpoint),
            Config.IndexName,
            new DefaultAzureCredential()
        );
    }

    public async Task<string> SearchAsync(string query)
    {
        var results = await _searchClient.SearchAsync<SearchDocument>(query, new SearchOptions { Size = 3 });

        var chunks = new List<string>();
        await foreach (var result in results.Value.GetResultsAsync())
        {
            chunks.Add(result.Document["content"].ToString()!);
        }

        return string.Join("\n\n", chunks);
    }
}
