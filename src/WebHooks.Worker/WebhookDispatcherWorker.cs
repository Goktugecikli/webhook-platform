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

    private static TimeSpan GetRetryDelay(int retryCount)
    {
        return retryCount switch
        {
            0 => TimeSpan.FromSeconds(10),   // 1. retry
            1 => TimeSpan.FromSeconds(30),   // 2. retry
            2 => TimeSpan.FromMinutes(2),    // 3. retry
            3 => TimeSpan.FromMinutes(10),   // 4. retry
            _ => TimeSpan.FromMinutes(30)    // sonrası
        };
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var now = DateTime.UtcNow;

                // ✅ 1) Published olanları al
                // ✅ 2) Failed olup retry zamanı gelmiş olanları da al
                var candidates = await db.WebhookDeliveries
                    .Where(x =>
                        x.Status == WebhookDeliveryStatus.Published ||
                        (x.Status == WebhookDeliveryStatus.Failed && x.NextRetryAt != null && x.NextRetryAt <= now)
                    )
                    .OrderBy(x => x.CreatedAt)
                    .Take(10)
                    .ToListAsync(stoppingToken);

                if (candidates.Count == 0)
                {
                    _logger.LogInformation("No deliveries to dispatch.");
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }

                _logger.LogInformation("Found {Count} deliveries to dispatch.", candidates.Count);

                foreach (var d in candidates)
                {
                    // Processing
                    d.MarkProcessing();
                }

                // tek seferde kaydet (performans)
                await db.SaveChangesAsync(stoppingToken);

                foreach (var d in candidates)
                {
                    try
                    {
                        // Şimdilik gerçek HTTP yok -> başarılı varsay
                        _logger.LogInformation("Dispatching delivery {Id} ({Provider}/{EventType})",
                            d.Id, d.Provider, d.EventType);

                        d.MarkSucceeded();
                    }
                    catch (Exception ex)
                    {
                        var retryDelay = GetRetryDelay(d.RetryCount);

                        // max retry sayısı (örnek: 5)
                        if (d.RetryCount >= 5)
                        {
                            _logger.LogWarning(
                                "Delivery {Id} moved to DEAD after {RetryCount} retries",
                                d.Id,
                                d.RetryCount
                            );

                            d.MarkDead(ex.Message);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Delivery {Id} failed. Retry #{RetryCount} scheduled after {Delay}",
                                d.Id,
                                d.RetryCount + 1,
                                retryDelay
                            );

                            d.MarkFailed(ex.Message, retryDelay);
                        }
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
