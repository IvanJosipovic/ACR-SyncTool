namespace ACR_SyncTool.DockerClient.Authentication;

public class AnonymousOAuthAuthenticationProvider : AuthenticationProvider
{
    private readonly OAuthClient _client = new OAuthClient();

    private static string Schema { get; } = "Bearer";

    public override async Task AuthenticateAsync(HttpRequestMessage request, HttpResponseMessage response)
    {
        var header = TryGetSchemaHeader(response, Schema);

        //Get the bearer bits
        var bearerBits = AuthenticateParser.ParseTyped(header.Parameter);

        //Get the token
        var token = await _client.GetTokenAsync(
                        bearerBits.Realm,
                        bearerBits.Service,
                        bearerBits.Scope);

        //Set the header
        request.Headers.Authorization = new AuthenticationHeaderValue(Schema, token.Token);
    }

    public override HttpClientHandler UpdateHttpClientHandler(HttpClientHandler httpClientHandler)
    {
        return httpClientHandler;
    }
}