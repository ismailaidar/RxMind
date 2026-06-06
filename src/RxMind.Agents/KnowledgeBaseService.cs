using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace RxMind.Agents;

public record RetrievedChunk(string Content, double Score);

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
        var chunks = await SearchRawAsync(query, size: 3);
        return string.Join("\n\n", chunks.Select(c => c.Content));
    }

    // Returns individual chunks with relevance scores — used by the eval harness.
    public async Task<IReadOnlyList<RetrievedChunk>> SearchRawAsync(string query, int size = 3)
    {
        var results = await _searchClient.SearchAsync<SearchDocument>(
            query, new SearchOptions { Size = size });

        var chunks = new List<RetrievedChunk>();
        await foreach (var result in results.Value.GetResultsAsync())
            chunks.Add(new RetrievedChunk(result.Document["content"].ToString()!, result.Score ?? 0));

        return chunks;
    }
}
