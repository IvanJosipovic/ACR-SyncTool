using ACR_SyncTool;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(configBuilder =>
    {
        configBuilder.AddInMemoryCollection(new List<KeyValuePair<string, string>>()
        {
            { new KeyValuePair<string, string>("Logging:LogLevel:Default", "Information") },
            { new KeyValuePair<string, string>("Logging:LogLevel:Microsoft", "Warning") },
            { new KeyValuePair<string, string>("Logging:LogLevel:Microsoft.Hosting.Lifetime", "Warning") },
        });
    })
    .ConfigureServices((hostBuilder, services) =>
    {
        services.AddHostedService<Worker>();
        services.AddSingleton<SyncTool>();
    })
    .Build();

await host.RunAsync();
