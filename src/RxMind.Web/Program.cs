using DotNetEnv;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using System.Threading.RateLimiting;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Wire Entra ID values from .env into the config system
builder.Configuration["AzureAd:TenantId"] = Environment.GetEnvironmentVariable("ENTRA_TENANT_ID");
builder.Configuration["AzureAd:ClientId"] = Environment.GetEnvironmentVariable("ENTRA_CLIENT_ID");
builder.Configuration["AzureAd:ClientSecret"] = Environment.GetEnvironmentVariable("ENTRA_CLIENT_SECRET");
builder.Configuration["AzureAd:Instance"] = "https://login.microsoftonline.com/";
builder.Configuration["AzureAd:CallbackPath"] = "/signin-oidc";

// Entra ID authentication
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// Rate limiting — max 5 requests per user per minute
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("perUser", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Application Insights — tracks auth failures, page load latency, errors
builder.Configuration["ApplicationInsights:ConnectionString"] =
    Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
builder.Services.AddApplicationInsightsTelemetry();

// HttpClient for calling RxMind.Api
builder.Services.AddHttpClient("RxMindApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5033");
    client.Timeout = TimeSpan.FromMinutes(3); // agents are slow
});

builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI(); // adds /MicrosoftIdentity/Account/SignIn and SignOut routes

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages().RequireAuthorization(); // all pages require login

app.Run();
