namespace ACR_SyncTool.DockerClient.Authentication;

/// <summary>
/// Authentication provider.
/// </summary>
public abstract class AuthenticationProvider
{
    /// <summary>
    /// Called when the send is challenged.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="response"></param>
    /// <returns></returns>
    public abstract Task AuthenticateAsync(HttpRequestMessage request, HttpResponseMessage response);

    /// <summary>
    /// Called on initial creation of the HttpClientHandler
    /// </summary>
    /// <param name="httpClientHandler"></param>
    /// <returns></returns>
    public abstract HttpClientHandler UpdateHttpClientHandler(HttpClientHandler httpClientHandler);

    /// <summary>
    /// Gets the schema header from the http response.
    /// </summary>
    /// <param name="response"></param>
    /// <param name="schema"></param>
    /// <returns></returns>
    protected AuthenticationHeaderValue TryGetSchemaHeader(HttpResponseMessage response, string schema)
    {
        var header = GetHeaderBySchema(response, schema);

        if (header == null)
        {
            throw new InvalidOperationException(
                $"No WWW-Authenticate challenge was found for schema {schema}");
        }

        return header;
    }

    public static AuthenticationHeaderValue? GetHeaderBySchema(HttpResponseMessage response, string schema)
    {
        if (response == null) throw new ArgumentNullException(nameof(response));

        return response.Headers.WwwAuthenticate.FirstOrDefault(s => s.Scheme == schema);
    }
}