namespace CsvDataUploader.Options;

public class SqlOptions
{

    // can fully configure the connection string yourself
    public string ConnectionString { get; set; } = string.Empty;

    // required if not using preconfigured connection string
    public string Endpoint { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;

    // leave blank if you want to use managed identity
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    // include for multi-tenant managed identity scenarios
    public string TenantId { get; set; } = string.Empty;
    public bool ClearDatabaseDataBeforeRun { get; set; } = false;
}
