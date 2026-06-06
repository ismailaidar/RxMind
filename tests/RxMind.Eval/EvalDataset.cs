using System.Text.Json;

namespace RxMind.Eval;

public record EvalCase(string Id, string Query, string[] RelevantKeywords, string Description);

public static class EvalDataset
{
    public static List<EvalCase> Load(string path)
    {
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        return JsonSerializer.Deserialize<List<EvalCase>>(json, options)!;
    }

    // A chunk is relevant if it contains any of the expected keywords (case-insensitive).
    public static bool IsRelevant(string chunkContent, string[] keywords)
        => keywords.Any(kw => chunkContent.Contains(kw, StringComparison.OrdinalIgnoreCase));
}
