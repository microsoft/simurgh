namespace CsvDataUploader;

public class UploaderOptions
{
    // if using managed identity, can be used to specify tenant for multi-tenant dev accounts
    public string TenantId { get; set; } = string.Empty;
    // leave blank if you want to use the key or managed identity
    public string ConnectionString { get; set; } = string.Empty;

    // use this if you want to use the endpoint and key or endpoint and managed identity
    public string Endpoint { get; set; } = string.Empty;
    // leave blank if you want to use the connection string or managed identity
    public string Key { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string PartitionKey { get; set; } = "partitionKey";
    public string CsvFilePath { get; set; } = string.Empty;
    // how many records to upload at once
    public int BatchSize { get; set; } = 5;
}
