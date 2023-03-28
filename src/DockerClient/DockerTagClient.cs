using System.Web;

namespace ACR_SyncTool.DockerClient;

/// <summary>
/// https://docs.docker.com/registry/spec/api
/// </summary>
public class DockerTagClient
{
    public ILogger<DockerTagClient> Logger { get; set; }

    public string Host;

    public AuthenticationProvider AuthenticationProvider;

    public bool Https { get; set; } = true;

    public DockerTagClient(ILogger<DockerTagClient> logger)
    {
        this.Logger = logger;
        this.AuthenticationProvider = new AnonymousOAuthAuthenticationProvider();
    }

    public async Task<List<string>> GetTags(string image)
    {
        var tags = new List<string>();

        tags.AddRange(await GetTagsRecursive($"{(Https ? "https" : "http")}://{Host}/v2/{image}/tags/list"));

        return tags;
    }

    private async Task<List<string>> GetTagsRecursive(string url)
    {
        var tags = new List<string>();

        var handler = new HttpClientHandler();

        handler = AuthenticationProvider.UpdateHttpClientHandler(handler);

        var httpClient = new HttpClient(handler);

        var request = CreateRequest(url);

        HttpStatusCode[] httpStatusCodesWorthRetrying = {
           HttpStatusCode.RequestTimeout, // 408
           HttpStatusCode.TooManyRequests, // 429
           HttpStatusCode.InternalServerError, // 500
           HttpStatusCode.BadGateway, // 502
           HttpStatusCode.ServiceUnavailable, // 503
           HttpStatusCode.GatewayTimeout // 504
        };

        var response = await Policy
              .HandleResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
              .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
              .ExecuteAsync(async () => await httpClient.SendAsync(request));

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var request2 = CreateRequest(url);

            await AuthenticationProvider.AuthenticateAsync(request2, response);

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

                if (Uri.TryCreate(pageQuery, new UriCreationOptions(), out var result))
                {
                    if (result.IsAbsoluteUri)
                    {
                        pageQuery = result.PathAndQuery;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(2));

                List<string> newTags;

                newTags = await GetTagsRecursive($"{(Https ? "https" : "http")}://{Host}{pageQuery}");

                tags.AddRange(newTags);
            }

            if (responseContent != null)
            {
                tags.AddRange(responseContent.Tags);
            }

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
        public string Name { get; set; } = string.Empty;

        public string[] Tags { get; set; } = Array.Empty<string>();
    }
}
