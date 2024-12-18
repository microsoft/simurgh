namespace CsvDataUploader.Options;

internal class TextAnalyticsServiceOptions
{
    // include if using managed identity in multi-tenant environment
    public string TenantId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    // leave blank if using managed identity
    public string Key { get; set; } = string.Empty;

    public bool UseExponentialRetryPolicy { get; set; } = true;
    public int DelayInSeconds { get; set; } = 2;
    public int MaxDelayInSeconds { get; set; } = 16;
    public int MaxRetries { get; set; } = 5;
}
