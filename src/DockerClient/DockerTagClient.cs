namespace ACR_SyncTool.DockerClient;

/// <summary>
/// https://docs.docker.com/registry/spec/api
/// </summary>
public class DockerTagClient
{
    private readonly string host;

    private readonly AuthenticationProvider authenticationProvider;

    public bool Https { get; set; } = true;

    public DockerTagClient(string host)
    {
        this.host = host;
        this.authenticationProvider = new AnonymousOAuthAuthenticationProvider();
    }

    public DockerTagClient(string host, AuthenticationProvider authenticationProvider)
    {
        this.host = host;
        this.authenticationProvider = authenticationProvider;
    }

    public async Task<List<string>> GetTags(string image)
    {
        var tags = new List<string>();

        tags.AddRange(await GetTagsRecursive($"{(Https ? "https" : "http")}://{host}/v2/{image}/tags/list"));

        return tags;
    }

    private async Task<List<string>> GetTagsRecursive(string url)
    {
        var tags = new List<string>();

        var handler = new HttpClientHandler();

        handler = authenticationProvider.UpdateHttpClientHandler(handler);

        var httpClient = new HttpClient(handler);

        var request = CreateRequest(url);

        var response = await httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var request2 = CreateRequest(url);

            await authenticationProvider.AuthenticateAsync(request2, response);

            response = await httpClient.SendAsync(request2);
        }

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadFromJsonAsync<ListImageTagsResponse>();

            if (response.Headers.Contains("link"))
            {
                var link = response.Headers.GetValues("link").First();

                var matches = Regex.Match(link, "<(.+)>; rel=\"next\"");

                var pageQuery = matches.Groups[1].Value;

                var newTags = await GetTagsRecursive($"{(Https ? "https" : "http")}://{host}{pageQuery}");

                tags.AddRange(newTags);
            }

            tags.AddRange(responseContent.Tags);

            return tags;
        }

        throw new Exception($"Error making http call {response.StatusCode} - {url} - {await response.Content.ReadAsStringAsync()}");
    }

    private HttpRequestMessage CreateRequest(string url)
    {
        return new HttpRequestMessage()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url)
        };
    }

    private class ListImageTagsResponse
    {
        public string Name { get; set; }

        public string[] Tags { get; set; }
    }
}