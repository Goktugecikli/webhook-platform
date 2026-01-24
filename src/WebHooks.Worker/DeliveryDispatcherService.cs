using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebHooks.Infrastructre.Persistence;
using WebHooks.Domain;

public sealed class DeliveryDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeliveryDispatcherService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private const int BatchSize = 50;

    public DeliveryDispatcherService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<DeliveryDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claimed = await ClaimBatch(stoppingToken);

                if (claimed.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                foreach (var deliveryId in claimed)
                {
                    await DispatchOne(deliveryId, stoppingToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatcher loop error");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private async Task<List<Guid>> ClaimBatch(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        using var tx = await db.Database.BeginTransactionAsync(ct);

        var eligible = await db.WebhookDeliveries
            .Where(x =>
                (x.Status == WebhookDeliveryStatus.Published || x.Status == WebhookDeliveryStatus.Failed) &&
                (x.NextRetryAt == null || x.NextRetryAt <= now)
            )
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (eligible.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return new List<Guid>();
        }

        foreach (var d in eligible)
            d.MarkProcessing();

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return eligible.Select(x => x.Id).ToList();
    }

    private async Task DispatchOne(Guid deliveryId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = _httpClientFactory.CreateClient("webhooks");

        var d = await db.WebhookDeliveries.FirstOrDefaultAsync(x => x.Id == deliveryId, ct);
        if (d is null) return;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, d.TargetUrl);
            req.Content = new StringContent(d.Payload);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // (Opsiyonel) correlation headers
            req.Headers.TryAddWithoutValidation("X-Webhook-Provider", d.Provider);
            req.Headers.TryAddWithoutValidation("X-Webhook-EventType", d.EventType);
            req.Headers.TryAddWithoutValidation("Idempotency-Key", d.IdempotencyKey);

            var resp = await client.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300)
            {
                d.MarkSucceeded((int)resp.StatusCode, body);
            }
            else
            {
                var delay = RetryPolicy.GetDelay(d.RetryCount + 1);
                if (RetryPolicy.IsDead(d.RetryCount + 1))
                    d.MarkDead($"HTTP {(int)resp.StatusCode}", (int)resp.StatusCode, body);
                else
                    d.MarkFailed($"HTTP {(int)resp.StatusCode}", delay, (int)resp.StatusCode, body);
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            var delay = RetryPolicy.GetDelay(d.RetryCount + 1);
            if (RetryPolicy.IsDead(d.RetryCount + 1))
                d.MarkDead(ex.Message);
            else
                d.MarkFailed(ex.Message, delay);

            await db.SaveChangesAsync(ct);
        }
    }
}

public static class RetryPolicy
{
    // attemptIndex: 1..n (RetryCount+1 gibi düşün)
    public static TimeSpan GetDelay(int attemptIndex) =>
        attemptIndex switch
        {
            1 => TimeSpan.FromSeconds(5),
            2 => TimeSpan.FromSeconds(30),
            3 => TimeSpan.FromMinutes(2),
            4 => TimeSpan.FromMinutes(10),
            5 => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromHours(2)
        };

    public static bool IsDead(int attemptIndex) => attemptIndex >= 6;
}
