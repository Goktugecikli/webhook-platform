using WebHooks.Infrastructre;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddInfrastructure(context.Configuration);

        services.AddHttpClient("webhooks", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHostedService<DeliveryDispatcherService>();
    })
    .RunConsoleAsync();
