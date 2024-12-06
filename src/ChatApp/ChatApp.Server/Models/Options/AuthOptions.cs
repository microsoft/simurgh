using System.Text.Json.Serialization;

namespace ChatApp.Server.Models.Options;

public class AuthOptions
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;
    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
    [JsonPropertyName("grantType")]
    public string GrantType { get; set; } = string.Empty;
}
