namespace ChatApp.Server.Models.Options;

public class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string Deployment { get; set; } = string.Empty;
    public string APIKey { get; set; } = string.Empty;
}
