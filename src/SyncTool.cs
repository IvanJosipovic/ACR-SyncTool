using Docker.Registry.DotNet;
using Docker.Registry.DotNet.Authentication;
using Docker.Registry.DotNet.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Azure.Containers.ContainerRegistry;
using Azure.Identity;
using Azure;
using ACR_SyncTool.Models;
using System.Text.Json;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace ACR_SyncTool
{
    public class SyncTool
    {
        private readonly ILogger<SyncTool> logger;

        private readonly IConfiguration configuration;

        private const string ImageRegex = @"(^([a-zA-Z0-9_.-]+)\/((?:[a-z0-9_.-]+\/?)+))(?::?([a-z0-9_.-]+))?$";

        public SyncTool(ILogger<SyncTool> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
        }

        private string GetHostImage(string image)
        {
            var match = Regex.Match(image, ImageRegex);

            if (!match.Success)
            {
                throw new Exception($"Image format is not valid: '{image}'. Should be host/repository/image");
            }

            return match.Groups[1].Value;
        }

        private string GetHost(string image)
        {
            var match = Regex.Match(image, ImageRegex);

            if (!match.Success)
            {
                throw new Exception($"Image format is not valid: '{image}'. Should be host/repository/image");
            }

            return match.Groups[2].Value;
        }

        private string GetImage(string image)
        {
            var match = Regex.Match(image, ImageRegex);

            if (!match.Success)
            {
                throw new Exception($"Image format is not valid: '{image}'. Should be host/repository/image");
            }

            return match.Groups[3].Value;
        }

        private string GetTag(string image)
        {
            var match = Regex.Match(image, ImageRegex);

            if (!match.Success)
            {
                throw new Exception($"Image format is not valid: '{image}'. Should be host/repository/image");
            }

            return match.Groups[4].Value;
        }

        private List<string> GetSyncedImages()
        {
            return configuration.GetSection("SyncedImages").Get<List<string>>();
        }

        private IRegistryClient GetClient(string host)
        {
            var regClientConfig = new RegistryClientConfiguration(host);

            var registryConfig = GetRegistryConfig(host);

            if (registryConfig == null || registryConfig.AuthType == null)
            {
                return regClientConfig.CreateClient();
            }

            return registryConfig.AuthType switch
            {
                "Basic" => regClientConfig.CreateClient(new BasicAuthenticationProvider(registryConfig.Username, registryConfig.Password)),
                "PasswordOAuth" => regClientConfig.CreateClient(new PasswordOAuthAuthenticationProvider(registryConfig.Username, registryConfig.Password)),
                "AnonymousOAuth" => regClientConfig.CreateClient(new AnonymousOAuthAuthenticationProvider()),
                _ => throw new Exception($"Unknown AuthType: {registryConfig.AuthType}"),
            };
        }

        private RegistryConfig? GetRegistryConfig(string host)
        {
            return configuration.GetSection("Registries").Get<List<RegistryConfig>>().Find(x => x.Host == host);
        }

        private ACRConfig GetACRConfig(string host)
        {
            var acrConfig = configuration.GetSection("AzureContainerRegistries").Get<List<ACRConfig>>().Find(x => x.Host == host);

            if (acrConfig == null)
            {
                throw new Exception($"Azure Container Registry Configuration not found for {host}");
            }

            return acrConfig;
        }

        private async Task<List<string>> GetTags(string image)
        {
            using var client = GetClient(GetHost(image));

            var tags = await client.Tags.ListImageTagsAsync(GetImage(image));

            return tags.Tags.ToList();
        }

        public void ExportExistingImages(string acrHostName, string jsonExportFilePath)
        {
            logger.LogInformation("{0} - {1} - Starting", DateTimeOffset.Now, nameof(ExportExistingImages));

            var acrConfig = GetACRConfig(acrHostName);

            var client = new ContainerRegistryClient(
                new Uri("https://" + acrHostName),
                new ClientSecretCredential(acrConfig.TenantId, acrConfig.ClientId, acrConfig.Secret),
                new ContainerRegistryClientOptions()
                {
                    Audience = ContainerRegistryAudience.AzureResourceManagerPublicCloud
                });

            var export = new List<ImageExport>();

            var repositryNames = client.GetRepositoryNames();
            foreach (string repositryName in repositryNames)
            {
                var imageExport = new ImageExport
                {
                    Image = repositryName
                };

                var repo = client.GetRepository(repositryName);

                var manifests = repo.GetManifestPropertiesCollection();
                foreach (var manifest in manifests)
                {
                    if (manifest.Tags.Count > 0)
                    {
                        imageExport.Tags.Add(manifest.Tags[0]);
                    }
                }

                export.Add(imageExport);
            }

            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(jsonExportFilePath, json);

            logger.LogInformation("{0} - {1} - Ending", DateTimeOffset.Now, nameof(ExportExistingImages));
        }

        public async Task PullAndSaveMissingImages(string jsonExportFilePath, string imageTarFilePath)
        {
            logger.LogInformation("{0} - {1} - Starting", DateTimeOffset.Now, nameof(PullAndSaveMissingImages));

            if (!File.Exists(jsonExportFilePath))
            {
                logger.LogWarning("{0} - {1} - Existing Image File doesn't exist!", DateTimeOffset.Now, nameof(LoadAndPushImages));
                return;
            }

            var imageToPull = GetSyncedImages();
            var existingImages = JsonSerializer.Deserialize<List<ImageExport>>(File.OpenRead(jsonExportFilePath));

            var dockerClient = new DockerClientConfiguration().CreateClient();

            var savedImages = new List<string>();

            foreach (var image in imageToPull)
            {
                var tags = await GetTags(image);
                var existingImage = existingImages?.FirstOrDefault(x => x.Image == image);

                var registryConfig = GetRegistryConfig(GetHost(image));

                foreach (var tag in tags)
                {
                    if (existingImage == null || !existingImage.Tags.Contains(tag))
                    {
                        logger.LogInformation("{0} - {1} - Pulling {2}:{3}", DateTimeOffset.Now, nameof(PullAndSaveMissingImages), image, tag);

                        savedImages.Add($"{image}:{tag}");

                        var authConfig = new AuthConfig();
                        if (registryConfig != null)
                        {
                            authConfig.Username = registryConfig.Username;
                            authConfig.Password = registryConfig.Password;
                        }

                        await dockerClient.Images.CreateImageAsync(
                            new ImagesCreateParameters
                            {
                                FromImage = image,
                                Tag = tag,
                            },
                            authConfig,
                            new Progress<JSONMessage>());
                    }
                }
            }

            if (savedImages.Count > 0)
            {
                logger.LogInformation("{0} - {1} - Saving Images to Disk", nameof(PullAndSaveMissingImages), DateTimeOffset.Now);
                using FileStream outputFileStream = new FileStream(imageTarFilePath, FileMode.Create);
                var fileStream = await dockerClient.Images.SaveImagesAsync(savedImages.ToArray());
                await fileStream.CopyToAsync(outputFileStream);
            }
            else
            {
                logger.LogInformation("{0} - {1} - No new Images to save", nameof(PullAndSaveMissingImages), DateTimeOffset.Now);
            }

            logger.LogInformation("{0} - {1} - Ending", DateTimeOffset.Now, nameof(PullAndSaveMissingImages));
        }

        public async Task LoadAndPushImages(string imageTarFilePath, string acrHostName)
        {
            logger.LogInformation("{0} - {1} - Starting", DateTimeOffset.Now, nameof(LoadAndPushImages));

            if (!File.Exists(imageTarFilePath))
            {
                logger.LogWarning("{0} - {1} - Input File doesn't exist!", DateTimeOffset.Now, nameof(LoadAndPushImages));
                return;
            }

            var dockerClient = new DockerClientConfiguration().CreateClient();

            using var input = File.OpenRead(imageTarFilePath);

            var progressJSONMessage = new Progress<JSONMessage>();

            logger.LogInformation("{0} - {1} - Loading Images", DateTimeOffset.Now, nameof(LoadAndPushImages));

            await dockerClient.Images.LoadImageAsync(new ImageLoadParameters(), input, progressJSONMessage);

            var images = await dockerClient.Images.ListImagesAsync(new ImagesListParameters() { All = true });

            var registryConfig = GetACRConfig(acrHostName);

            var syncedImages = GetSyncedImages();

            foreach (var image in images)
            {
                if (!syncedImages.Contains(GetHostImage(image.RepoTags[0])))
                {
                    continue;
                }

                logger.LogInformation("{0} - {1} - Pushing Image {2}", DateTimeOffset.Now, nameof(LoadAndPushImages), $"{acrHostName}/{GetHostImage(image.RepoTags[0])}:{GetTag(image.RepoTags[0])}");

                await dockerClient.Images.TagImageAsync(image.RepoTags[0], new ImageTagParameters() { RepositoryName = $"{acrHostName}/{GetHostImage(image.RepoTags[0])}", Tag = GetTag(image.RepoTags[0]) });

                await dockerClient.Images.PushImageAsync(
                    $"{acrHostName}/{GetHostImage(image.RepoTags[0])}",
                    new ImagePushParameters() { Tag = GetTag(image.RepoTags[0]) },
                    new AuthConfig()
                    {
                        Username = registryConfig.ClientId,
                        Password = registryConfig.Secret
                    },
                    progressJSONMessage);
            }

            logger.LogInformation("{0} - {1} - Ending", DateTimeOffset.Now, nameof(LoadAndPushImages));
        }
    }
}
