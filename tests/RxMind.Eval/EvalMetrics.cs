namespace RxMind.Eval;

public static class EvalMetrics
{
    // Did any of the top-k chunks match? (binary per query)
    // retrieval is broken => no useful content at all
    public static bool HitAtK(IReadOnlyList<bool> relevance, int k)
        => relevance.Take(k).Any(r => r);

    // 1 / rank of the first relevant chunk. 0 if none found.
    // relevant content exists but ranks low => LLM sees noise first
    public static double ReciprocalRank(IReadOnlyList<bool> relevance)
    {
        for (int i = 0; i < relevance.Count; i++)
            if (relevance[i]) return 1.0 / (i + 1);
        return 0;
    }

    // How many of the top-k slots are relevant?
    // too many irrelevant chunks => wasting the agent's context window
    public static double PrecisionAtK(IReadOnlyList<bool> relevance, int k)
    {
        var top = relevance.Take(k).ToList();
        return top.Count == 0 ? 0 : top.Count(r => r) / (double)k;
    }

    public static double Mean(IEnumerable<double> values)
        => values.DefaultIfEmpty(0).Average();
}
