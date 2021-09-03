using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ACR_SyncTool.DockerClient
{
    [DataContract]
    internal class OAuthToken
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("issued_at")]
        public DateTime IssuedAt { get; set; }
    }
}