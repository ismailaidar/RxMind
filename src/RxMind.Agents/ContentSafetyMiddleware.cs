using Azure.AI.ContentSafety;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace RxMind.Agents;

// Sits between the workflow and the LLM — scans every user message before it reaches the model.
public class ContentSafetyMiddleware : DelegatingChatClient
{
    private readonly ContentSafetyClient _safetyClient;

    public ContentSafetyMiddleware(IChatClient innerClient) : base(innerClient)
    {
        _safetyClient = new ContentSafetyClient(
            new Uri(Config.ContentSafetyEndpoint),
            new DefaultAzureCredential()
        );
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var flaggedCategory = await GetFlaggedCategoryAsync(messages, cancellationToken);
        if (flaggedCategory != null)
        {
            return new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                $"I'm unable to process this request. Content was flagged for: {flaggedCategory}."));
        }

        return await base.GetResponseAsync(messages, options, cancellationToken);
    }

    private async Task<string?> GetFlaggedCategoryAsync(IEnumerable<ChatMessage> messages, CancellationToken ct)
    {
        // Only scan the latest user message — agent-to-agent messages were already checked
        var userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
        if (userMessage == null || string.IsNullOrEmpty(userMessage.Text))
            return null;

        var result = await _safetyClient.AnalyzeTextAsync(new AnalyzeTextOptions(userMessage.Text), ct);

        // Severity: 0 = safe, 2 = low, 4 = medium, 6 = high — block anything above safe
        return result.Value.CategoriesAnalysis
            .FirstOrDefault(c => c.Severity >= 2)
            ?.Category.ToString();
    }
}
