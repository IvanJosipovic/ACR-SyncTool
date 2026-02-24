public class Worker : BackgroundService
{
    private readonly ILogger<Worker> logger;
    private readonly IConfiguration configuration;
    private readonly IHostApplicationLifetime appLifetime;
    private readonly IHostEnvironment hostEnvironment;
    private readonly SyncTool syncTool;

    public Worker(ILogger<Worker> logger, IConfiguration configuration, IHostApplicationLifetime appLifetime, IHostEnvironment hostEnvironment, SyncTool syncTool)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.appLifetime = appLifetime;
        this.hostEnvironment = hostEnvironment;
        this.syncTool = syncTool;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!((ConfigurationRoot)configuration).Providers.Any(x => x is JsonConfigurationProvider { Source.Path: "appsettings.json" }))
            {
                logger.LogError("Missing appsettings.json");
                Environment.ExitCode = 1;
                appLifetime.StopApplication();
                return;
            }

            string action = configuration.GetValue<string>("Action");
            string acrHostName = configuration.GetValue<string>("ACRHostName");

            string jsonExportExistingFilePath = configuration.GetValue<string>("JsonExportExistingFilePath");
            string jsonExportMissingFilePath = configuration.GetValue<string>("JsonExportMissingFilePath");

            if (string.IsNullOrEmpty(action))
            {
                logger.LogError("--Action parameter is missing");
                Environment.ExitCode = 1;
                appLifetime.StopApplication();
                return;
            }

            if ((action == nameof(SyncTool.ExportExistingImages) || action == nameof(SyncTool.ImportMissingImages)) && string.IsNullOrEmpty(acrHostName))
            {
                logger.LogError("--ACRHostName parameter is missing");
                Environment.ExitCode = 1;
                appLifetime.StopApplication();
                return;
            }

            if ((action == nameof(SyncTool.ExportExistingImages) || action == nameof(SyncTool.ExportMissingImages)) && string.IsNullOrEmpty(jsonExportExistingFilePath))
            {
                logger.LogError("--JsonExportExistingFilePath parameter is missing");
                Environment.ExitCode = 1;
                appLifetime.StopApplication();
                return;
            }

            if ((action == nameof(SyncTool.ExportMissingImages) || action == nameof(SyncTool.ImportMissingImages)) && string.IsNullOrEmpty(jsonExportMissingFilePath))
            {
                logger.LogError("--JsonExportMissingFilePath parameter is missing");
                Environment.ExitCode = 1;
                appLifetime.StopApplication();
                return;
            }

            logger.LogInformation("SyncTool {version} started at: {time}", FileVersionInfo.GetVersionInfo(GetType().Assembly.Location).ProductVersion, DateTimeOffset.Now);

            switch (action)
            {
                case nameof(SyncTool.ExportExistingImages):
                    syncTool.ExportExistingImages(acrHostName, jsonExportExistingFilePath);
                    break;
                case nameof(SyncTool.ExportMissingImages):
                    await syncTool.ExportMissingImages(jsonExportExistingFilePath, jsonExportMissingFilePath);
                    break;
                case nameof(SyncTool.ImportMissingImages):
                    await syncTool.ImportMissingImages(acrHostName, jsonExportMissingFilePath);
                    break;
                default:
                    throw new Exception($"Unknown Action: {action}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SyncTool Fatal Error");
            Environment.ExitCode = 1;
        }

        logger.LogInformation("SyncTool complated at: {time}", DateTimeOffset.Now);
        appLifetime.StopApplication();
    }
}
