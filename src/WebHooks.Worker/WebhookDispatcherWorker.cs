using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebHooks.Domain;
using WebHooks.Infrastructre.Persistence;

public class WebhookDispatcherWorker : BackgroundService
{
    private readonly ILogger<WebhookDispatcherWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public WebhookDispatcherWorker(ILogger<WebhookDispatcherWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    private static TimeSpan GetRetryDelay(int retryIndex)
    {
        return retryIndex switch
        {
            0 => TimeSpan.FromSeconds(10),
            1 => TimeSpan.FromSeconds(30),
            2 => TimeSpan.FromMinutes(2),
            3 => TimeSpan.FromMinutes(10),
            _ => TimeSpan.FromMinutes(30)
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook Worker started.");

        const int BatchSize = 10;
        const int MaxRetries = 5;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var now = DateTime.UtcNow;

                // Published olanlar + retry zamanı gelmiş Failed olanlar
                var candidates = await db.WebhookDeliveries
                    .Where(x =>
                        x.Status == WebhookDeliveryStatus.Published ||
                        (x.Status == WebhookDeliveryStatus.Failed && x.NextRetryAt != null && x.NextRetryAt <= now)
                    )
                    .OrderBy(x => x.CreatedAt)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                if (candidates.Count == 0)
                {
                    _logger.LogDebug("No deliveries to dispatch.");
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }

                _logger.LogInformation("Found {Count} deliveries to dispatch.", candidates.Count);

                // Önce hepsini Processing'e al (attempt++ burada olacak)
                foreach (var d in candidates)
                    d.MarkProcessing();

                await db.SaveChangesAsync(stoppingToken);

                // Sonra tek tek dispatch et
                foreach (var d in candidates)
                {
                    try
                    {
                        _logger.LogInformation("Dispatching delivery {Id} ({Provider}/{EventType})",
                            d.Id, d.Provider, d.EventType);

                        // TODO: Gerçek HTTP dispatch burada olacak
                        // Şimdilik "başarılı" simülasyonu
                        var fakeStatusCode = 200;
                        var fakeResponse = "OK";

                        d.MarkSucceeded(fakeStatusCode, fakeResponse);
                    }
                    catch (Exception ex)
                    {
                        // Bu failure ile birlikte kaçıncı retry olacak?
                        var nextRetryCount = d.RetryCount + 1;

                        // Max retry aşıldıysa Dead
                        if (nextRetryCount > MaxRetries)
                        {
                            _logger.LogWarning(
                                "Delivery {Id} moved to DEAD after {RetryCount} retries. Error: {Error}",
                                d.Id, d.RetryCount, ex.Message);

                            d.MarkDead(ex.Message);
                            continue;
                        }

                        // retryIndex 0 tabanlı (0 => ilk retry)
                        var retryDelay = GetRetryDelay(d.RetryCount);

                        _logger.LogWarning(
                            "Delivery {Id} failed. Scheduling Retry #{NextRetry} after {Delay}. Error: {Error}",
                            d.Id, nextRetryCount, retryDelay, ex.Message);

                        d.MarkFailed(ex.Message, retryDelay);
                    }
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatch cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
