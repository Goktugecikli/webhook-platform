using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using WebHooks.Infrastructre;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddInfrastructure(context.Configuration);
        services.AddHostedService<WebhookDispatcherWorker>();
    })
    .RunConsoleAsync();
