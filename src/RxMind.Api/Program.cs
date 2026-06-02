using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using RxMind.Agents;
using System.Threading.RateLimiting;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Application Insights — tracks LLM errors, latency, failed retrievals
builder.Configuration["ApplicationInsights:ConnectionString"] =
    Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
builder.Services.AddApplicationInsightsTelemetry();

// Wire Entra ID values for API token validation
builder.Configuration["AzureAd:TenantId"] = Environment.GetEnvironmentVariable("ENTRA_TENANT_ID");
builder.Configuration["AzureAd:ClientId"] = Environment.GetEnvironmentVariable("ENTRA_CLIENT_ID");
builder.Configuration["AzureAd:Instance"] = "https://login.microsoftonline.com/";

// API validates bearer tokens issued by Entra ID
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7000")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Rate limiting — 5 requests per user per minute on the /process endpoint
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("perUser", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 2,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddSingleton<RxMindWorkflow>();

var app = builder.Build();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

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

// Requires valid Entra ID bearer token — rejects any call without Authorization header
app.MapPost("/process", async (RxMindWorkflow wf, PatientRequest request) =>
{
    var result = await wf.ProcessAsync(request.Input);
    return Results.Ok(new { response = result });
}).RequireAuthorization()
  .RequireRateLimiting("perUser");

app.Run();

record PatientRequest(string Input);
