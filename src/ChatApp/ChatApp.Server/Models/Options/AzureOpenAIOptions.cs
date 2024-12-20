namespace ChatApp.Server.Models.Options;

public class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ChatDeployment { get; set; } = string.Empty;
    public string EmbeddingsDeployment { get; set; } = string.Empty;
    public string APIKey { get; set; } = string.Empty;
}
