using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using CsvDataUploader.Options;
using OpenAI.Embeddings;

namespace CsvDataUploader.Services;

internal class VectorizationService
{
    private readonly EmbeddingClient _client;
    public VectorizationService(VectorizationOptions options)
    {
        if (!Uri.TryCreate(options.Endpoint, UriKind.RelativeOrAbsolute, out var uri)) throw new ArgumentException($"{nameof(options.Endpoint)} is not a valid URI.");

        AzureOpenAIClient client;

        // I think there is a nifty way to set retry policy here but a bit involved
        if (string.IsNullOrWhiteSpace(options.Key))
        {
            client = string.IsNullOrWhiteSpace(options.TenantId)
                ? new AzureOpenAIClient(uri, new DefaultAzureCredential())
                : new AzureOpenAIClient(uri, new DefaultAzureCredential(new DefaultAzureCredentialOptions()
                {
                    TenantId = options.TenantId
                }));
        }
        else
            client = new AzureOpenAIClient(uri, new AzureKeyCredential(options.Key));

        _client = client.GetEmbeddingClient(options.Deployment);
    }

    // todo: add test connction capability to validate permissions etc. before processing all csv records

    public async Task<ReadOnlyMemory<float>?> GetEmbeddingAsync(string text)
    {
        var result = await _client.GenerateEmbeddingAsync(text);

        return result?.Value?.ToFloats();
    }
}
