using DotNetEnv;

namespace RxMind.Agents;

public static class Config
{
    static Config() { Env.TraversePath().Load(); }

    public static string FoundryEndpoint =>
        Environment.GetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT")
        ?? throw new InvalidOperationException("AZURE_FOUNDRY_ENDPOINT is not set.");

    public static string ModelDeployment =>
        Environment.GetEnvironmentVariable("AZURE_MODEL_DEPLOYMENT") ?? "gpt-4.1-mini";
}
