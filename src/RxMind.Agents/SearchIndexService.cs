using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

namespace RxMind.Agents;

public class SearchIndexService
{
    private const string IndexName = "rxmind-knowledge";
    private const int ChunkSize = 500; // words per chunk

    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;

    public SearchIndexService()
    {
        var endpoint = new Uri(Config.SearchEndpoint);
        var credential = new DefaultAzureCredential();

        _indexClient = new SearchIndexClient(endpoint, credential);
        _searchClient = new SearchClient(endpoint, IndexName, credential);
    }

    public async Task EnsureIndexExistsAsync()
    {
        //  the check for index throws if not exists, forced into try/catch.
        try
        {
            await _indexClient.GetIndexAsync(IndexName);
            return; // already exists
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // doesn't exist yet — create it
        }

        var index = new SearchIndex(IndexName)
        {
            Fields =
            {
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                new SearchableField("content")
            }
        };

        await _indexClient.CreateIndexAsync(index);
    }

    public async Task IndexDocumentsAsync(string markdownText)
    {
        var chunks = ChunkText(markdownText, ChunkSize);

        var documents = chunks.Select((chunk, i) => new
        {
            id = $"chunk-{i}",
            content = chunk
        });

        await _searchClient.UploadDocumentsAsync(documents); // upload documents to azure AI search
    }

    private static List<string> ChunkText(string text, int chunkSize)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();

        for (int i = 0; i < words.Length; i += chunkSize)
        {
            var chunk = string.Join(' ', words.Skip(i).Take(chunkSize));
            chunks.Add(chunk);
        }

        return chunks;
    }
}
