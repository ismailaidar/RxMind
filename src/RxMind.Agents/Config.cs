using DotNetEnv;

namespace RxMind.Agents;

public static class Config
{
    static Config() { Env.TraversePath().Load(); }

    public static string OpenAIEndpoint =>
        Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
        ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

    public static string ModelDeployment =>
        Environment.GetEnvironmentVariable("AZURE_MODEL_DEPLOYMENT") ?? "gpt-4.1-mini";

    public static string SearchEndpoint =>
        Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT")
        ?? throw new InvalidOperationException("AZURE_SEARCH_ENDPOINT is not set.");

    public static string ContentUnderstandingEndpoint =>
        Environment.GetEnvironmentVariable("CONTENT_UNDERSTANDING_ENDPOINT")
        ?? throw new InvalidOperationException("CONTENT_UNDERSTANDING_ENDPOINT is not set.");
}
