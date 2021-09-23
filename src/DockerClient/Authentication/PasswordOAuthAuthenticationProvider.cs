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

        if (header.Parameter == null)
        {
            throw new Exception("Berer details missing in header");
        }

        //Get the bearer bits
        var bearerBits = AuthenticateParser.ParseTyped(header.Parameter);

        //Get the token
        var token = await _client.GetTokenAsync(
                        bearerBits.Realm,
                        bearerBits.Service,
                        bearerBits.Scope,
                        _username,
                        _password);

        //Set the header
        request.Headers.Authorization = new AuthenticationHeaderValue(Schema, token.Token);
    }

    public override HttpClientHandler UpdateHttpClientHandler(HttpClientHandler httpClientHandler)
    {
        return httpClientHandler;
    }
}