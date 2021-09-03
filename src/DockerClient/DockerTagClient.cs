using ACR_SyncTool.DockerClient.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace ACR_SyncTool.DockerClient
{
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

            var handler = new HttpClientHandler();

            handler = authenticationProvider.UpdateHttpClientHandler(handler);

            var httpClient = new HttpClient(handler);

            var request = CreateRequest(image);

            var response = await httpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var request2 = CreateRequest(image);

                await authenticationProvider.AuthenticateAsync(request2, response);

                response = await httpClient.SendAsync(request2);
            }

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadFromJsonAsync<ListImageTagsResponse>();

                tags.AddRange(responseContent.Tags);
            }

            return tags;
        }

        private HttpRequestMessage CreateRequest(string image)
        {
            return new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{(Https ? "https" : "http")}://{host}/v2/{image}/tags/list")
            };
        }

        private class ListImageTagsResponse
        {
            public string Name { get; set; }

            public string[] Tags { get; set; }
        }
    }
}
