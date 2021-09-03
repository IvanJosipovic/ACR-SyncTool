using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace ACR_SyncTool.DockerClient.Authentication
{
    public class ClientCertificateAuthenticationProvider : AuthenticationProvider
    {
        private readonly string _path;

        private readonly string _key;

        public ClientCertificateAuthenticationProvider(string path, string key)
        {
            _path = path;
            _key = key;
        }

        public override Task AuthenticateAsync(HttpRequestMessage request, HttpResponseMessage response)
        {
            return Task.CompletedTask;
        }

        public override HttpClientHandler UpdateHttpClientHandler(HttpClientHandler httpClientHandler)
        {
            var cert = new X509Certificate2(_path, _key);
            httpClientHandler.ClientCertificates.Add(cert);
            //httpClientHandler.ClientCertificateOptions = ClientCertificateOption.Automatic;

            httpClientHandler.SslProtocols = SslProtocols.Tls12;
            httpClientHandler.ClientCertificateOptions = ClientCertificateOption.Automatic;
            return httpClientHandler;
        }
    }
}