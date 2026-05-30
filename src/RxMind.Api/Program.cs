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

// Register RxMindWorkflow as a singleton
builder.Services.AddSingleton<RxMindWorkflow>();

var app = builder.Build();
app.UseCors();

// RxMindWorkflow automatically injected into the endpoint handler by DI
app.MapPost("/process", async (RxMindWorkflow wf, PatientRequest request) =>
{
    var result = await wf.ProcessAsync(request.Input);
    return Results.Ok(new { response = result });
});

app.Run();

record PatientRequest(string Input);
