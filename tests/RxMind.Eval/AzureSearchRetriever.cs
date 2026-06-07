using RetriEval.Core;
using RxMind.Agents;
using RxMindChunk = RxMind.Agents.RetrievedChunk;

namespace RxMind.Eval;

// Bridges KnowledgeBaseService into the RetriEval.Core IRetriever contract.
public class AzureSearchRetriever(KnowledgeBaseService kb) : IRetriever
{
    public async Task<IReadOnlyList<RetriEval.Core.RetrievedChunk>> RetrieveAsync(
        string query, int k, CancellationToken ct = default)
    {
        var chunks = await kb.SearchRawAsync(query, k);
        return chunks.Select((c, i) => new RetriEval.Core.RetrievedChunk(
            Id:       $"chunk-{i}",
            Content:  c.Content,
            Score:    c.Score,
            Metadata: null
        )).ToList();
    }
}
