using DotNetEnv;
using RxMind.Agents;
using Xunit;

namespace RxMind.Eval;

// Runs all queries once and shares results across every test in this class.
public class EvalFixture : IAsyncLifetime
{
    public List<QueryResult> Results { get; } = new();
    public bool IsConfigured { get; private set; }
    public string ReportPath { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        Env.TraversePath().Load();
        IsConfigured = !string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT"));

        if (!IsConfigured) return;

        var kb = new KnowledgeBaseService();
        var dataPath = Path.Combine(AppContext.BaseDirectory, "data", "golden_dataset.json");
        var dataset = EvalDataset.Load(dataPath);

        foreach (var c in dataset)
        {
            var chunks = await kb.SearchRawAsync(c.Query, size: 3);
            var relevance = chunks
                .Select(chunk => EvalDataset.IsRelevant(chunk.Content, c.RelevantKeywords))
                .ToList();

            Results.Add(new QueryResult(
                c.Id, c.Query,
                chunks.Select(ch => ch.Content).ToList(),
                chunks.Select(ch => ch.Score).ToList(),
                relevance));
        }

        var reporter = new EvalReporter();
        foreach (var r in Results) reporter.Add(r);
        ReportPath = Path.Combine(AppContext.BaseDirectory, "eval_report.md");
        reporter.WriteTo(ReportPath);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class RetrievalEvalTests(EvalFixture fixture) : IClassFixture<EvalFixture>
{
    // Gates: tests fail (and block CI) when retrieval regresses past these thresholds.
    private const double HitAtThreeThreshold   = 0.70; // ≥70% of queries find a relevant chunk in top 3
    private const double MrrThreshold          = 0.50; // first relevant result is on average rank 2
    private const double PrecisionAtKThreshold = 0.40; // ≥40% of returned slots are relevant

    [SkippableFact]
    public void HitAtThree_AtLeast70Percent()
    {
        Skip.If(!fixture.IsConfigured, "AZURE_SEARCH_ENDPOINT not set — skipping eval");
        var score = EvalMetrics.Mean(
            fixture.Results.Select(r => EvalMetrics.HitAtK(r.Relevance, 3) ? 1.0 : 0.0));
        Assert.True(score >= HitAtThreeThreshold,
            $"Hit@3 = {score:P1} — below {HitAtThreeThreshold:P0} threshold. " +
            $"See {fixture.ReportPath} for per-query breakdown.");
    }

    [SkippableFact]
    public void MRR_AtLeastPoint5()
    {
        Skip.If(!fixture.IsConfigured, "AZURE_SEARCH_ENDPOINT not set — skipping eval");
        var score = EvalMetrics.Mean(
            fixture.Results.Select(r => EvalMetrics.ReciprocalRank(r.Relevance)));
        Assert.True(score >= MrrThreshold,
            $"MRR = {score:F3} — below {MrrThreshold:F2} threshold.");
    }

    [SkippableFact]
    public void PrecisionAtThree_AtLeastPoint4()
    {
        Skip.If(!fixture.IsConfigured, "AZURE_SEARCH_ENDPOINT not set — skipping eval");
        var score = EvalMetrics.Mean(
            fixture.Results.Select(r => EvalMetrics.PrecisionAtK(r.Relevance, 3)));
        Assert.True(score >= PrecisionAtKThreshold,
            $"P@3 = {score:F3} — below {PrecisionAtKThreshold:F2} threshold.");
    }

    [SkippableFact]
    public void NoQueryReturnsZeroChunks()
    {
        Skip.If(!fixture.IsConfigured, "AZURE_SEARCH_ENDPOINT not set — skipping eval");
        var empty = fixture.Results
            .Where(r => r.Chunks.Count == 0)
            .Select(r => $"{r.Id}: {r.Query}")
            .ToList();
        Assert.True(empty.Count == 0,
            $"Queries returned zero chunks — index may be empty or unreachable:\n" +
            string.Join("\n", empty));
    }
}
