using DotNetEnv;
using RetriEval.Core;
using RxMind.Agents;
using Xunit;

namespace RxMind.Eval;

public class EvalFixture : IAsyncLifetime
{
    public static readonly MetricThresholds Thresholds = new()
    {
        HitAtK       = 0.70,
        Mrr          = 0.50,
        PrecisionAtK = 0.40
    };

    public EvalReport? Report { get; private set; }
    public bool IsConfigured { get; private set; }
    public string ReportPath { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        Env.TraversePath().Load();
        IsConfigured = !string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT"));

        if (!IsConfigured) return;

        var retriever = new AzureSearchRetriever(new KnowledgeBaseService());
        var runner    = new EvalRunner(
            retriever,
            KeywordGrader.Instance,
            new EvalOptions { K = 3, Observer = new ConsoleEvalObserver() });

        var dataPath = Path.Combine(AppContext.BaseDirectory, "data", "golden_dataset.json");
        var dataset  = await GoldenSetLoader.LoadAsync(dataPath);

        Report = await runner.RunAsync(dataset);

        ReportPath = Path.Combine(AppContext.BaseDirectory, "eval_report.md");
        await new MarkdownReporter(new MarkdownReporterOptions
        {
            Thresholds = Thresholds
        }).WriteAsync(Report, ReportPath);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class RetrievalEvalTests(EvalFixture fixture) : IClassFixture<EvalFixture>
{
    private static readonly MetricThresholds Thresholds = EvalFixture.Thresholds;

    [SkippableFact]
    public void HitAtThree_AtLeast70Percent()
    {
        Skip.If(!fixture.IsConfigured, "AZURE_SEARCH_ENDPOINT not set — skipping eval");
        var score = fixture.Report!.Aggregate.HitAtK;
        Assert.True(score >= Thresholds.HitAtK,
            $"Hit@3 = {score:P1} — below {Thresholds.HitAtK:P0} threshold. " +
            $"See {fixture.ReportPath}");
    }

    [SkippableFact]
    public void MRR_AtLeastPoint5()
    {
        Skip.If(!fixture.IsConfigured, "AZURE_SEARCH_ENDPOINT not set — skipping eval");
        var score = fixture.Report!.Aggregate.Mrr;
        Assert.True(score >= Thresholds.Mrr,
            $"MRR = {score:F3} — below {Thresholds.Mrr:F2} threshold.");
    }

    [SkippableFact]
    public void PrecisionAtThree_AtLeastPoint4()
    {
        Skip.If(!fixture.IsConfigured, "AZURE_SEARCH_ENDPOINT not set — skipping eval");
        var score = fixture.Report!.Aggregate.MeanPrecisionAtK;
        Assert.True(score >= Thresholds.PrecisionAtK,
            $"P@3 = {score:F3} — below {Thresholds.PrecisionAtK:F2} threshold.");
    }

    [SkippableFact]
    public void NoQueryReturnsZeroChunks()
    {
        Skip.If(!fixture.IsConfigured, "AZURE_SEARCH_ENDPOINT not set — skipping eval");
        var empty = fixture.Report!.Results
            .Where(r => r.Retrieved.Count == 0)
            .Select(r => $"{r.CaseId}: {r.Query}")
            .ToList();
        Assert.True(empty.Count == 0,
            $"Queries returned zero chunks — index may be empty or unreachable:\n" +
            string.Join("\n", empty));
    }
}
