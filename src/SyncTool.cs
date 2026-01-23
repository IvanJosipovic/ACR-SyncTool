using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerRegistry;
using Azure.ResourceManager.ContainerRegistry.Models;

public class SyncTool
{
    private readonly ILogger<SyncTool> logger;

    private readonly IConfiguration configuration;

    private readonly IServiceProvider serviceProvider;

    private const string ImageRegex = @"(^([a-zA-Z0-9_.-]+)\/((?:[a-z0-9_.-]+\/?)+))(?::?([a-z0-9_.-]+))?$";

    public SyncTool(ILogger<SyncTool> logger, IConfiguration configuration, IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.serviceProvider = serviceProvider;
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
        return configuration.GetSection("Registries").Get<List<RegistryConfig>>()?.FirstOrDefault(x => x.Host == host);
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

        DockerTagClient? client = serviceProvider.GetRequiredService<DockerTagClient>();

        if (registryConfig == null || registryConfig.AuthType == null)
        {
            client.Host = GetHost(image);
        }
        else
        {
            client.Host = GetHost(image);

            switch (registryConfig.AuthType)
            {
                case "Basic":
                    client.AuthenticationProvider = new BasicAuthenticationProvider(registryConfig.Username, registryConfig.Password);
                    break;
                case "PasswordOAuth":
                    client.AuthenticationProvider = new PasswordOAuthAuthenticationProvider(registryConfig.Username, registryConfig.Password);
                        break;
                case "AnonymousOAuth":
                    client.AuthenticationProvider = new AnonymousOAuthAuthenticationProvider();
                    break;
                default:
                    new Exception($"Unknown AuthType: {registryConfig.AuthType}");
                    break;
            };
        }

        return await client.GetTags(GetImage(image));
    }

    public void ExportExistingImages(string acrHostName, string jsonExportExistingFilePath)
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

            foreach (var manifest in repo.GetAllManifestProperties())
            {
                if (manifest.Tags.Count > 0)
                {
                    imageExport.Tags.Add(manifest.Tags[0]);
                }
            }

            export.Add(imageExport);
        }

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions() { WriteIndented = true });

        File.WriteAllText(jsonExportExistingFilePath, json);

        logger.LogInformation("{0} - {1} - Ending", DateTimeOffset.Now, nameof(ExportExistingImages));
    }

    public async Task ExportMissingImages(string jsonExportExistingFilePath, string jsonExportMissingFilePath)
    {
        logger.LogInformation("{0} - {1} - Starting", DateTimeOffset.Now, nameof(ExportMissingImages));

        if (!File.Exists(jsonExportExistingFilePath))
        {
            logger.LogWarning("{0} - {1} - Existing Image File doesn't exist!", DateTimeOffset.Now, nameof(ExportMissingImages));
            return;
        }

        var existingImages = JsonSerializer.Deserialize<List<ImageExport>>(File.OpenRead(jsonExportExistingFilePath));

        var savedImages = new List<string>();

        foreach (SyncedImage image in GetSyncedImages())
        {
            var existingImage = existingImages?.FirstOrDefault(x => x.Image == image.Image);

            var registryConfig = GetRegistryConfig(GetHost(image.Image));

            List<string>? tags;

            if (image.Tags.Count > 0)
            {
                tags = image.Tags;
            }
            else
            {
                tags = await GetTags(image.Image);
            }

            foreach (var tag in tags)
            {
                if (!string.IsNullOrEmpty(image.Semver) && !new SemanticVersioning.Range(image.Semver).IsSatisfied(tag))
                {
                    logger.LogDebug("{0} - {1} - Skipped to due Semver {2} {0}:{1}", DateTimeOffset.Now, nameof(ExportExistingImages), image.Semver, image.Image, tag);
                    continue;
                }

                if (!string.IsNullOrEmpty(image.Regex) && !Regex.IsMatch(tag, image.Regex))
                {
                    logger.LogDebug("{0} - {1} - Skipped to due Regex {2} {0}:{1}", DateTimeOffset.Now, nameof(ExportExistingImages), image.Regex, image.Image, tag);
                    continue;
                }

                if (existingImage == null || !existingImage.Tags.Contains(tag))
                {
                    logger.LogInformation("{0} - {1} - To Save {2}:{3}", DateTimeOffset.Now, nameof(ExportExistingImages), image.Image, tag);

                    savedImages.Add($"{image.Image}:{tag}");
                }
            }

            if (savedImages.Count != 0)
            {
                var json = JsonSerializer.Serialize(savedImages, new JsonSerializerOptions() { WriteIndented = true });

                await File.WriteAllTextAsync(jsonExportMissingFilePath, json);
            }
        }

        logger.LogInformation("{0} - {1} - Ending", DateTimeOffset.Now, nameof(ExportExistingImages));
    }

    public async Task ImportMissingImages(string acrHostName, string jsonExportMissingFilePath)
    {
        logger.LogInformation("{0} - {1} - Starting", DateTimeOffset.Now, nameof(ImportMissingImages));

        var missingImages = await JsonSerializer.DeserializeAsync<List<string>>(File.OpenRead(jsonExportMissingFilePath));

        var acrConfig = GetACRConfig(acrHostName);

        var cred = new ClientSecretCredential(acrConfig.TenantId, acrConfig.ClientId, acrConfig.Secret);
        var arm = new ArmClient(cred);

        var targetId = ContainerRegistryResource.CreateResourceIdentifier(acrConfig.SubscriptionId, acrConfig.ResourceGroupName, acrConfig.Host.Replace(".azurecr.io", ""));
        var target = arm.GetContainerRegistryResource(targetId);

        foreach (var image in missingImages)
        {
            var importSource = new ContainerRegistryImportSource(GetImage(image) + ":" + GetTag(image))
            {
                RegistryAddress = GetHost(image),
            };

            var registryConfig = GetRegistryConfig(GetHost(image));

            if (registryConfig != null && !string.IsNullOrEmpty(registryConfig.Username) && !string.IsNullOrEmpty(registryConfig.Password))
            {
                importSource.Credentials = new ContainerRegistryImportSourceCredentials(registryConfig.Password) { Username = registryConfig.Username };
            }

            var content = new ContainerRegistryImportImageContent(importSource)
            {
                Mode = ContainerRegistryImportMode.Force
            };

            content.TargetTags.Add(image);

            logger.LogInformation("{0} - {1} - Importing {2}", DateTimeOffset.Now, nameof(ImportMissingImages), image);
            await target.ImportImageAsync(WaitUntil.Completed, content);
        }

        logger.LogInformation("{0} - {1} - Ending", DateTimeOffset.Now, nameof(ImportMissingImages));
    }
}
