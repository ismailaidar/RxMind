using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using System.Net.Http.Json;

namespace RxMind.Web.Pages;

[Authorize]
[EnableRateLimiting("perUser")]
public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
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
            var client = _httpClientFactory.CreateClient("RxMindApi");
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
