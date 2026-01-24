using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class WebhookDispatcherWorker : BackgroundService
{
    private readonly ILogger<WebhookDispatcherWorker> _logger;
    private readonly WebhookDispatcher _dispatcher;

    public WebhookDispatcherWorker(ILogger<WebhookDispatcherWorker> logger, WebhookDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        const int BatchSize = 10;
        const int MaxRetries = 5;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await _dispatcher.RunOnce(BatchSize, MaxRetries, stoppingToken);
                if (processed == 0)
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatch cycle failed");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}