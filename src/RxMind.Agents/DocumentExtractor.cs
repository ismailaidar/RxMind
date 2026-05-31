using Azure;
using Azure.AI.ContentUnderstanding;
using Azure.Identity;

namespace RxMind.Agents;

public class DocumentExtractor
{
    private readonly ContentUnderstandingClient _client;

    public DocumentExtractor()
    {
        _client = new ContentUnderstandingClient(
            new Uri(Config.ContentUnderstandingEndpoint),
            new DefaultAzureCredential()
        );
    }

    public async Task<string> ExtractAsync(string filePath)
    {
        var binaryData = BinaryData.FromBytes(await File.ReadAllBytesAsync(filePath));

        // sends the PDF to Azure and returns an operation. WaitUntil.Started means don't block, just start it.
        var operation = await _client.AnalyzeBinaryAsync(
            WaitUntil.Started,
            "prebuilt-read",
            binaryData,
            contentType: "application/pdf"
        );

        while (!operation.HasCompleted)
        {
            await Task.Delay(2000); //check HasCompleted every 2s while azure is proccessing the pdf on the could 
            await operation.UpdateStatusAsync();
        }

        return operation.Value.Contents[0].Markdown ?? string.Empty;
    }
}
