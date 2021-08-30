using System.Diagnostics;
using System.Reflection;

namespace ACR_SyncTool;

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
            if (!File.Exists(Path.Combine(hostEnvironment.ContentRootPath, "appsettings.json")))
            {
                logger.LogError("missing appsettings.json");
                Environment.ExitCode = 1;
                appLifetime.StopApplication();
                return;
            }

            string action = configuration.GetValue<string>("Action");
            string acrHostName = configuration.GetValue<string>("ACRHostName");
            string imageTarFilePath = configuration.GetValue<string>("ImagesTarFilePath");
            string jsonExportFilePath = configuration.GetValue<string>("JsonExportFilePath");

            if (string.IsNullOrEmpty(action))
            {
                logger.LogError("--Action parameter is missing");
                Environment.ExitCode = 1;
                appLifetime.StopApplication();
                return;
            }

            if ((action == nameof(SyncTool.ExportExistingImages) || action == nameof(SyncTool.LoadAndPushImages)) && string.IsNullOrEmpty(acrHostName))
            {
                logger.LogError("--ACRHostName parameter is missing");
                Environment.ExitCode = 1;
                appLifetime.StopApplication();
                return;
            }

            if ((action == nameof(SyncTool.PullAndSaveMissingImages) || action == nameof(SyncTool.LoadAndPushImages)) && string.IsNullOrEmpty(imageTarFilePath))
            {
                logger.LogError("--ImagesTarFilePath parameter is missing");
                Environment.ExitCode = 1;
                appLifetime.StopApplication();
                return;
            }

            if ((action == nameof(SyncTool.ExportExistingImages) || action == nameof(SyncTool.PullAndSaveMissingImages)) && string.IsNullOrEmpty(jsonExportFilePath))
            {
                logger.LogError("--JsonExportFilePath parameter is missing");
                Environment.ExitCode = 1;
                appLifetime.StopApplication();
                return;
            }

            logger.LogInformation("SyncTool {version} started at: {time}", FileVersionInfo.GetVersionInfo(GetType().Assembly.Location).ProductVersion, DateTimeOffset.Now);

            switch (action)
            {
                case nameof(SyncTool.ExportExistingImages):
                    syncTool.ExportExistingImages(acrHostName, jsonExportFilePath);
                    break;
                case nameof(SyncTool.PullAndSaveMissingImages):
                    await syncTool.PullAndSaveMissingImages(jsonExportFilePath, imageTarFilePath);
                    break;
                case nameof(SyncTool.LoadAndPushImages):
                    await syncTool.LoadAndPushImages(imageTarFilePath, acrHostName);
                    break;
                default:
                    throw new Exception($"Unknown Action: {action}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SyncTool Fatal Error");
            Environment.ExitCode = 1;
            appLifetime.StopApplication();
            return;
        }

        logger.LogInformation("SyncTool complated at: {time}", DateTimeOffset.Now);
        Environment.ExitCode = 0;
        appLifetime.StopApplication();
    }
}
