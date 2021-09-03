using System.Text.RegularExpressions;
using Azure.Containers.ContainerRegistry;
using Azure.Identity;
using ACR_SyncTool.Models;
using System.Text.Json;
using Docker.DotNet;
using Docker.DotNet.Models;
using ACR_SyncTool.DockerClient;
using ACR_SyncTool.DockerClient.Authentication;

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

        private List<SyncedImage> GetSyncedImages()
        {
            return configuration.GetSection("SyncedImages").Get<List<SyncedImage>>();
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
            var registryConfig = GetRegistryConfig(GetHost(image));

            DockerTagClient? client = null;

            if (registryConfig == null || registryConfig.AuthType == null)
            {
                client = new DockerTagClient(GetHost(image));
            }
            else
            {
                switch (registryConfig.AuthType)
                {
                    case "Basic":
                        client = new DockerTagClient(GetHost(image), new BasicAuthenticationProvider(registryConfig.Username, registryConfig.Password));
                        break;
                    case "PasswordOAuth":
                        client = new DockerTagClient(GetHost(image), new PasswordOAuthAuthenticationProvider(registryConfig.Username, registryConfig.Password));
                        break;
                    case "AnonymousOAuth":
                        client = new DockerTagClient(GetHost(image), new AnonymousOAuthAuthenticationProvider());
                        break;
                    default:
                        throw new Exception($"Unknown AuthType: {registryConfig.AuthType}");
                }
            }

            return await client.GetTags(GetImage(image));
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

            foreach (string repositryName in client.GetRepositoryNames())
            {
                var imageExport = new ImageExport
                {
                    Image = repositryName
                };

                var repo = client.GetRepository(repositryName);

                foreach (var manifest in repo.GetManifestPropertiesCollection())
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

            var existingImages = JsonSerializer.Deserialize<List<ImageExport>>(File.OpenRead(jsonExportFilePath));

            var dockerClient = new DockerClientConfiguration().CreateClient();

            var savedImages = new List<string>();

            foreach (SyncedImage image in GetSyncedImages())
            {
                var currentImageSize = await GetCurrentImageSizesGB();

                if (currentImageSize > configuration.GetValue<double>("MaxSyncSizeGB"))
                {
                    logger.LogWarning("{0} - {1} - Reached Max Sync Size {2:D2}/{3}GB", DateTimeOffset.Now, nameof(PullAndSaveMissingImages), currentImageSize, configuration.GetValue<double>("MaxSyncSizeGB"));

                    break;
                }

                var existingImage = existingImages?.FirstOrDefault(x => x.Image == image.Image);

                var registryConfig = GetRegistryConfig(GetHost(image.Image));

                var tags = await GetTags(image.Image);

                foreach (var tag in tags)
                {
                    if (await GetCurrentImageSizesGB() > configuration.GetValue<double>("MaxSyncSizeGB"))
                    {
                        break;
                    }

                    if (!string.IsNullOrEmpty(image.Semver))
                    {
                        var range = new SemanticVersioning.Range(image.Semver);

                        if (!range.IsSatisfied(tag))
                        {
                            logger.LogDebug("{0} - {1} - Skipped to due Semver {2} {0}:{1}", DateTimeOffset.Now, nameof(PullAndSaveMissingImages), image.Semver, image.Image, tag);
                            continue;
                        }
                    }

                    if (!string.IsNullOrEmpty(image.Regex))
                    {
                        if (Regex.IsMatch(tag, image.Regex))
                        {
                            logger.LogDebug("{0} - {1} - Skipped to due Regex {2} {0}:{1}", DateTimeOffset.Now, nameof(PullAndSaveMissingImages), image.Regex, image.Image, tag);
                            continue;
                        }
                    }

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
                                FromImage = image.Image,
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
                if (image.RepoTags[0] == "<none>:<none>" || !syncedImages.Any(x => x.Image == GetHostImage(image.RepoTags[0])))
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

        private async Task<double> GetCurrentImageSizesGB()
        {
            var dockerClient = new DockerClientConfiguration().CreateClient();

            var images = await dockerClient.Images.ListImagesAsync(new ImagesListParameters() { All = true });

            return images.Where(x => x.RepoTags[0] != "<none>:<none>").Sum(x => x.Size) / 1000000000.00;
        }
    }
}
