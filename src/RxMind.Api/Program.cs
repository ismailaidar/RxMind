using DotNetEnv;
using RxMind.Agents;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

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
app.MapPost("/process", async (RxMindWorkflow wf, PatientRequest request) =>
{
    var result = await wf.ProcessAsync(request.Input);
    return Results.Ok(new { response = result });
});

app.Run();

record PatientRequest(string Input);
