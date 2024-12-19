using Azure;
using Azure.AI.TextAnalytics;
using Azure.Core;
using Azure.Identity;
using CsvDataUploader.Options;

namespace CsvDataUploader.Services;

internal class TextAnalyticsService
{
    private readonly TextAnalyticsClient _client;
    internal TextAnalyticsService(TextAnalyticsServiceOptions serviceOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceOptions.Endpoint, nameof(serviceOptions.Endpoint));
        if (!Uri.TryCreate(serviceOptions.Endpoint, UriKind.RelativeOrAbsolute, out var uri)) throw new ArgumentException($"{nameof(serviceOptions.Endpoint)} is not a valid URI.");

        // using exponential retry policy since we'll be perfomring a lot of requests
        var options = new TextAnalyticsClientOptions
        {
            Retry =
            {
                Mode = serviceOptions.UseExponentialRetryPolicy ? RetryMode.Exponential : RetryMode.Fixed,
                Delay = TimeSpan.FromSeconds(serviceOptions.DelayInSeconds),
                MaxDelay = TimeSpan.FromSeconds(serviceOptions.MaxDelayInSeconds),
                MaxRetries = serviceOptions.MaxRetries
            }
        };

        if (!string.IsNullOrWhiteSpace(serviceOptions.Key))
        {
            _client = new TextAnalyticsClient(uri, new AzureKeyCredential(serviceOptions.Key), options);
            return;
        }

        var defaultAzureCredential = string.IsNullOrWhiteSpace(serviceOptions.TenantId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions()
            {
                TenantId = serviceOptions.TenantId
            });

        _client = new TextAnalyticsClient(uri, defaultAzureCredential, options);
    }

    internal async Task<bool> TestConnectionAsync()
    {
        try
        {
            string document = "Hello world!";
            var response = await _client.DetectLanguageAsync(document);
            return true;
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return false;
        }
    }

    internal async Task<DocumentSentiment?> AnalyzeSentimentAsync(string text)
    {
        // todo: handle quota...
        var response = await _client.AnalyzeSentimentAsync(text);

        return response?.Value;
    }
}
