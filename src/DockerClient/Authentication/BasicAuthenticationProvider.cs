using System.Net.Http.Headers;
using System.Text;

namespace ACR_SyncTool.DockerClient.Authentication
{
    public class BasicAuthenticationProvider : AuthenticationProvider
    {
        private readonly string _password;

        private readonly string _username;

        public BasicAuthenticationProvider(string username, string password)
        {
            _username = username;
            _password = password;
        }

        private static string Schema { get; } = "Basic";

        public override Task AuthenticateAsync(HttpRequestMessage request, HttpResponseMessage response)
        {
            TryGetSchemaHeader(response, Schema);

            var passBytes = Encoding.UTF8.GetBytes($"{_username}:{_password}");
            var base64Pass = Convert.ToBase64String(passBytes);

            //Set the header
            request.Headers.Authorization = new AuthenticationHeaderValue(Schema, base64Pass);

            return Task.CompletedTask;
        }

        public override HttpClientHandler UpdateHttpClientHandler(HttpClientHandler httpClientHandler)
        {
            return httpClientHandler;
        }
    }
}