using System.Text.Json.Serialization;

namespace ChatApp.Server.Models
{
    // todo: see what we can remove from this and maintain feature parity in the UI
    public class EasyAuthUser
    {
        [JsonPropertyName("user_principal_id")]
        public string UserPrincipalId { get; set; } = "00000000-f021-4bd6-aa44-b324f63aaff5";
        [JsonPropertyName("user_name")]
        public string Username { get; set; } = "testusername@constoso.com";
        [JsonPropertyName("auth_provider")]
        public string AuthProvider { get; set; } = "aad";
        [JsonPropertyName("auth_token")]
        public string AuthToken { get; set; } = "your_aad_id_token";
        [JsonPropertyName("client_principal_b64")]
        public string ClientPrincipalB64 { get; set; } = "your_base_64_encoded_token";
        [JsonPropertyName("aad_id_token")]
        public string AadIdToken { get; set; } = "your_aad_id_token";
    }
}
