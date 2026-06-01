using DotNetEnv;
using RxMind.Agents;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Application Insights — tracks LLM errors, latency, failed retrievals
builder.Configuration["ApplicationInsights:ConnectionString"] =
    Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddSingleton<RxMindWorkflow>();

var app = builder.Build();
app.UseCors();

// Startup indexing — extract both PDFs and upload chunks to Azure AI Search
var filesRoot = Path.Combine(builder.Environment.ContentRootPath, "..", "RxMind.Agents", "files");

var indexService = new SearchIndexService();
bool indexCreated = await indexService.EnsureIndexExistsAsync();

// only runs on first start when the index doesn't exist yet
if (indexCreated)
{
    var extractor = new DocumentExtractor();

    var formularyText = await extractor.ExtractAsync(Path.Combine(filesRoot, "RxMind_Formulary.pdf"));
    await indexService.IndexDocumentsAsync(formularyText);

    var policiesText = await extractor.ExtractAsync(Path.Combine(filesRoot, "RxMind_Policies.pdf"));
    await indexService.IndexDocumentsAsync(policiesText);
}

// RxMindWorkflow automatically injected into the endpoint handler by DI
app.MapPost("/process", async (RxMindWorkflow wf, PatientRequest request, ILogger<Program> logger) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        logger.LogInformation("Processing request. InputLength={InputLength}", request.Input.Length);
        var result = await wf.ProcessAsync(request.Input);
        sw.Stop();
        logger.LogInformation("Request completed. Latency={LatencyMs}ms OutputLength={OutputLength}", sw.ElapsedMilliseconds, result.Length);
        return Results.Ok(new { response = result });
    }
    catch (Exception ex)
    {
        sw.Stop();
        logger.LogError(ex, "Request failed. Latency={LatencyMs}ms", sw.ElapsedMilliseconds);
        return Results.Problem("An error occurred processing your request.");
    }
});

app.Run();

record PatientRequest(string Input);
