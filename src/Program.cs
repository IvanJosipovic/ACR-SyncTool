IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(loggingBuilder =>
    {
        loggingBuilder.SetMinimumLevel(LogLevel.Debug);
        loggingBuilder.AddFilter("Default", LogLevel.Debug);
        loggingBuilder.AddFilter("Microsoft", LogLevel.Warning);
        loggingBuilder.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
    })
    .ConfigureServices((hostBuilder, services) =>
    {
        services.AddHostedService<Worker>();
        services.AddSingleton<SyncTool>();
    })
    .Build();

await host.RunAsync();
