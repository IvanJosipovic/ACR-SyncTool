namespace ACR_SyncTool.DockerClient.OAuth;

internal class OAuthClient
{
    private readonly HttpClient _client = new HttpClient();

    private async Task<OAuthToken> GetTokenInnerAsync(
        string realm,
        string service,
        string scope,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var queryString = new QueryString();

        queryString.AddIfNotEmpty("service", service);
        queryString.AddIfNotEmpty("scope", scope);

        var builder = new UriBuilder(new Uri(realm))
        {
            Query = queryString.GetQueryString()
        };

        var request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);

        if (username != null && password != null)
        {
            var bytes = Encoding.UTF8.GetBytes($"{username}:{password}");

            var parameter = Convert.ToBase64String(bytes);

            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", parameter);
        }

        using (var response = await _client.SendAsync(request, cancellationToken))
        {
            if (!response.IsSuccessStatusCode)
                throw new UnauthorizedAccessException("Unable to authenticate.");

            var body = await response.Content.ReadAsStringAsync();

            var token = JsonSerializer.Deserialize<OAuthToken>(body);

            if (token == null)
                throw new UnauthorizedAccessException("Unable to authenticate.");

            return token;
        }
    }

    public Task<OAuthToken> GetTokenAsync(
        string realm,
        string service,
        string scope,
        CancellationToken cancellationToken = default)
    {
        return this.GetTokenInnerAsync(realm, service, scope, null, null, cancellationToken);
    }

    public Task<OAuthToken> GetTokenAsync(
        string realm,
        string service,
        string scope,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        return this.GetTokenInnerAsync(
            realm,
            service,
            scope,
            username,
            password,
            cancellationToken);
    }
}