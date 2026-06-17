namespace ACR_SyncTool.DockerClient.OAuth;

internal class OAuthToken
{
    [JsonPropertyName("token")]
    public string? TokenValue { get; set; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonIgnore]
    public string Token => !string.IsNullOrWhiteSpace(AccessToken) ? AccessToken : TokenValue ?? string.Empty;
}