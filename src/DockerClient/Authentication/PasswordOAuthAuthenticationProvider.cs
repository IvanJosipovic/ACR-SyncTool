namespace ACR_SyncTool.DockerClient.Authentication;

public class PasswordOAuthAuthenticationProvider : AuthenticationProvider
{
    private readonly OAuthClient _client = new OAuthClient();

    private readonly string _password;

    private readonly string _username;

    public PasswordOAuthAuthenticationProvider(string username, string password)
    {
        _username = username;
        _password = password;
    }

    private static string Schema { get; } = "Bearer";

    public override async Task AuthenticateAsync(HttpRequestMessage request, HttpResponseMessage response)
    {
        var header = this.TryGetSchemaHeader(response, Schema);

        //Get the bearer bits
        var bearerBits = AuthenticateParser.ParseTyped(header.Parameter);

        //Get the token
        var token = await this._client.GetTokenAsync(
                        bearerBits.Realm,
                        bearerBits.Service,
                        bearerBits.Scope,
                        this._username,
                        this._password);

        //Set the header
        request.Headers.Authorization = new AuthenticationHeaderValue(Schema, token.Token);
    }

    public override HttpClientHandler UpdateHttpClientHandler(HttpClientHandler httpClientHandler)
    {
        return httpClientHandler;
    }
}