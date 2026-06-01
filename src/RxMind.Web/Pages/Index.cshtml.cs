using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RxMind.Web.Pages;

[Authorize]
[Authorize(Policy = "PharmacistOrAdmin")]
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

    [BindProperty]
    public string Input { get; set; } = string.Empty;

    public string? Response { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input))
        {
            ErrorMessage = "Please describe your prescription request.";
            return Page();
        }

        try
        {
            // Get access token for the API — Entra ID validates this on the API side
            var token = await _tokenAcquisition.GetAccessTokenForUserAsync([_apiScope]);

            var client = _httpClientFactory.CreateClient("RxMindApi");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var result = await client.PostAsJsonAsync("/process", new { input = Input });

            if (!result.IsSuccessStatusCode)
            {
                ErrorMessage = $"The pharmacy service returned an error ({(int)result.StatusCode}). Please try again.";
                return Page();
            }

            var body = await result.Content.ReadFromJsonAsync<ApiResponse>();
            Response = body?.response;
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

    private record ApiResponse(string response);
}
