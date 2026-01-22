using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using WebHooks.Domain;
using WebHooks.Infrastructre.Persistence;
using WebHooks.Infrastructre.Security;

public class WebhookDispatcherWorker : BackgroundService
{
    private readonly ILogger<WebhookDispatcherWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookDispatcherWorker(
        ILogger<WebhookDispatcherWorker> logger,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
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
        const int BatchSize = 10;
        const int MaxRetries = 5;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var now = DateTime.UtcNow;

                var candidates = await db.WebhookDeliveries
                    .Where(x =>
                        x.Status == WebhookDeliveryStatus.Published ||
                        (x.Status == WebhookDeliveryStatus.Failed &&
                         x.NextRetryAt != null &&
                         x.NextRetryAt <= now))
                    .OrderBy(x => x.CreatedAt)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                if (candidates.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }

                foreach (var d in candidates)
                    d.MarkProcessing();

                await db.SaveChangesAsync(stoppingToken);

                foreach (var d in candidates)
                {
                    try
                    {
                        var subscription = await db.WebhookSubscriptions
                            .Where(s => s.IsActive && s.Provider == d.Provider)
                            .AsNoTracking()
                            .ToListAsync(stoppingToken);

                        var matched = subscription
                            .Where(s =>
                                s.EventPrefix == "*" ||
                                d.EventType.StartsWith(s.EventPrefix, StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(s => s.EventPrefix == "*" ? 0 : s.EventPrefix.Length)
                            .FirstOrDefault();

                        if (matched is null)
                        {
                            d.MarkDead("No active subscription found");
                            continue;
                        }

                        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                        var signedPayload = $"{timestamp}.{d.Payload}";
                        var sigHex = WebhookSignature.ComputeSha256Hex(matched.Secret, signedPayload);

                        var http = _httpClientFactory.CreateClient("webhooks");

                        using var req = new HttpRequestMessage(HttpMethod.Post, matched.TargetUrl);
                        req.Content = new StringContent(d.Payload, Encoding.UTF8, "application/json");

                        req.Headers.TryAddWithoutValidation("X-Webhook-Id", d.Id.ToString());
                        req.Headers.TryAddWithoutValidation("X-Webhook-Timestamp", timestamp);
                        req.Headers.TryAddWithoutValidation("X-Webhook-Signature", $"sha256={sigHex}");
                        req.Headers.TryAddWithoutValidation("X-Webhook-Event", d.EventType);
                        req.Headers.TryAddWithoutValidation("X-Webhook-Provider", d.Provider);

                        using var res = await http.SendAsync(req, stoppingToken);
                        var resBody = await res.Content.ReadAsStringAsync(stoppingToken);

                        if ((int)res.StatusCode >= 200 && (int)res.StatusCode <= 299)
                        {
                            d.MarkSucceeded((int)res.StatusCode, resBody);
                        }
                        else
                        {
                            throw new WebhookDispatchHttpException((int)res.StatusCode, resBody);
                        }


                    }
                    catch (Exception ex)
                    {
                        var httpEx = ex as WebhookDispatchHttpException;

                        var nextRetryCount = d.RetryCount + 1;

                        if (nextRetryCount > MaxRetries)
                        {
                            d.MarkDead(
                                ex.Message,
                                httpEx?.StatusCode,
                                httpEx?.ResponseBody
                            );
                            continue;
                        }

                        var retryDelay = GetRetryDelay(nextRetryCount - 1);

                        d.MarkFailed(
                            ex.Message,
                            retryDelay,
                            httpEx?.StatusCode,
                            httpEx?.ResponseBody
                        );
                    }

                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatch cycle failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
