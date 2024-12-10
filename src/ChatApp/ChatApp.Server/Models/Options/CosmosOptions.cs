namespace ChatApp.Server.Models.Options;

public class CosmosOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string CosmosEndpoint { get; set; } = string.Empty;
    public string CosmosKey { get; set; } = string.Empty;
    public string CosmosDatabaseId { get; set; } = string.Empty;
    public string CosmosChatHistoryContainerId { get; set; } = string.Empty;
    public string CosmosStructuredDataContainerId { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}
