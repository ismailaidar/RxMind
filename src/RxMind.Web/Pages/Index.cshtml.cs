using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace RxMind.Web.Pages;

[Authorize]
[Authorize(Policy = "PharmacistOrAdmin")]
[AuthorizeForScopes(ScopeKeySection = "ApiScope")]
public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly string _apiScope;

    public IndexModel(IHttpClientFactory httpClientFactory, ITokenAcquisition tokenAcquisition)
    {
        _httpClientFactory = httpClientFactory;
        _tokenAcquisition = tokenAcquisition;
        _apiScope = $"api://{Environment.GetEnvironmentVariable("ENTRA_CLIENT_ID")}/process";
    }

    [BindProperty] public string PatientName { get; set; } = string.Empty;
    [BindProperty] public string Medication { get; set; } = string.Empty;
    [BindProperty] public string Dosage { get; set; } = string.Empty;
    [BindProperty] public string Diagnosis { get; set; } = string.Empty;
    [BindProperty] public string Insurance { get; set; } = string.Empty;
    [BindProperty] public string Prescriber { get; set; } = string.Empty;
    [BindProperty] public string Notes { get; set; } = string.Empty;

    public string? Response { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(PatientName) || string.IsNullOrWhiteSpace(Medication))
        {
            ErrorMessage = "Patient name and medication are required.";
            return Page();
        }

        var input = BuildInput();

        try
        {
            var token = await _tokenAcquisition.GetAccessTokenForUserAsync([_apiScope]);

            var client = _httpClientFactory.CreateClient("RxMindApi");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var result = await client.PostAsJsonAsync("/process", new { input });

            if (!result.IsSuccessStatusCode)
            {
                ErrorMessage = $"The pharmacy service returned an error ({(int)result.StatusCode}). Please try again.";
                return Page();
            }

            var body = await result.Content.ReadFromJsonAsync<ApiResponse>();
            Response = body?.response;
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "The request timed out. The AI agents are still processing — please try again.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not reach the pharmacy service: {ex.Message}";
        }

        return Page();
    }

    private string BuildInput()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Patient: {PatientName}");
        sb.AppendLine($"Medication: {Medication}");
        if (!string.IsNullOrWhiteSpace(Dosage))      sb.AppendLine($"Dosage: {Dosage}");
        if (!string.IsNullOrWhiteSpace(Diagnosis))   sb.AppendLine($"Diagnosis: {Diagnosis}");
        if (!string.IsNullOrWhiteSpace(Insurance))   sb.AppendLine($"Insurance: {Insurance}");
        if (!string.IsNullOrWhiteSpace(Prescriber))  sb.AppendLine($"Prescriber: {Prescriber}");
        if (!string.IsNullOrWhiteSpace(Notes))       sb.AppendLine($"Notes: {Notes}");
        return sb.ToString();
    }

    private record ApiResponse(string response);
}
