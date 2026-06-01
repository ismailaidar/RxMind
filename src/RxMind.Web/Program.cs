using DotNetEnv;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Wire Entra ID values from .env into the config system
builder.Configuration["AzureAd:TenantId"] = Environment.GetEnvironmentVariable("ENTRA_TENANT_ID");
builder.Configuration["AzureAd:ClientId"] = Environment.GetEnvironmentVariable("ENTRA_CLIENT_ID");
builder.Configuration["AzureAd:ClientSecret"] = Environment.GetEnvironmentVariable("ENTRA_CLIENT_SECRET");
builder.Configuration["AzureAd:Instance"] = "https://login.microsoftonline.com/";
builder.Configuration["AzureAd:CallbackPath"] = "/signin-oidc";

// The scope the Web app will request when getting a token to call the API
var apiScope = $"api://{Environment.GetEnvironmentVariable("ENTRA_CLIENT_ID")}/process";
builder.Configuration["ApiScope"] = apiScope; // read by [AuthorizeForScopes] on the Index page

// Entra ID authentication + token acquisition for downstream API calls
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi([apiScope])
    .AddInMemoryTokenCaches(); // cache tokens so we don't request a new one every call

// Application Insights — tracks auth failures, page load latency, errors
builder.Configuration["ApplicationInsights:ConnectionString"] =
    Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PharmacistOrAdmin", policy =>
        policy.RequireRole("Pharmacist", "Admin"));

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));
});

// HttpClient that automatically attaches Entra ID bearer token to every request
builder.Services.AddHttpClient("RxMindApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5033");
    client.Timeout = TimeSpan.FromMinutes(3);
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
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages().RequireAuthorization(); // all pages require login
app.MapControllers(); // required for MicrosoftIdentity sign-in/sign-out routes
app.Run();
