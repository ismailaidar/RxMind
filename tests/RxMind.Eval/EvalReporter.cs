using System.Text;

namespace RxMind.Eval;

public record QueryResult(
    string Id,
    string Query,
    List<string> Chunks,
    List<double> Scores,
    List<bool> Relevance);

public class EvalReporter
{
    private readonly List<QueryResult> _results = new();

    public void Add(QueryResult result) => _results.Add(result);

    public void WriteTo(string path)
    {
        var hit1  = _results.Select(r => EvalMetrics.HitAtK(r.Relevance, 1) ? 1.0 : 0.0);
        var hit3  = _results.Select(r => EvalMetrics.HitAtK(r.Relevance, 3) ? 1.0 : 0.0);
        var rrs   = _results.Select(r => EvalMetrics.ReciprocalRank(r.Relevance));
        var p3    = _results.Select(r => EvalMetrics.PrecisionAtK(r.Relevance, 3));

        var sb = new StringBuilder();
        sb.AppendLine("# RxMind Retrieval Eval Report");
        sb.AppendLine($"Run: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC  |  Queries: {_results.Count}");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine("| Metric | Score | Threshold | Pass? |");
        sb.AppendLine("|---|---|---|---|");
        AppendRow(sb, "Hit@1",  EvalMetrics.Mean(hit1),  format: "P1");
        AppendRow(sb, "Hit@3",  EvalMetrics.Mean(hit3),  threshold: 0.70, format: "P1");
        AppendRow(sb, "MRR",    EvalMetrics.Mean(rrs),   threshold: 0.50, format: "F3");
        AppendRow(sb, "P@3",    EvalMetrics.Mean(p3),    threshold: 0.40, format: "F3");
        sb.AppendLine();

        sb.AppendLine("## Per-query results");
        foreach (var r in _results)
        {
            var hit = EvalMetrics.HitAtK(r.Relevance, 3);
            var rr  = EvalMetrics.ReciprocalRank(r.Relevance);
            var p   = EvalMetrics.PrecisionAtK(r.Relevance, 3);
            sb.AppendLine($"### {r.Id}: {r.Query}");
            sb.AppendLine($"Hit@3: {(hit ? "✓" : "✗")}  |  RR: {rr:F2}  |  P@3: {p:F2}");
            sb.AppendLine();
            for (int i = 0; i < r.Chunks.Count; i++)
            {
                var tag     = i < r.Relevance.Count && r.Relevance[i] ? "RELEVANT" : "not relevant";
                var preview = r.Chunks[i].Length > 200 ? r.Chunks[i][..200] + "…" : r.Chunks[i];
                sb.AppendLine($"**Chunk {i + 1}** [{tag}] score={r.Scores[i]:F3}");
                sb.AppendLine($"> {preview}");
                sb.AppendLine();
            }
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static void AppendRow(StringBuilder sb, string metric, double value,
        double? threshold = null, string format = "F3")
    {
        var formatted = value.ToString(format);
        var thresh    = threshold.HasValue ? threshold.Value.ToString(format) : "—";
        var pass      = threshold.HasValue ? (value >= threshold.Value ? "✓" : "✗") : "—";
        sb.AppendLine($"| {metric} | {formatted} | {thresh} | {pass} |");
    }
}
