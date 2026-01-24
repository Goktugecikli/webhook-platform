using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using WebHooks.Domain;
using WebHooks.Infrastructre.Persistence;
using WebHooks.Infrastructre.Security;

public sealed class WebhookDispatcher
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<WebhookDispatcher> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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

    public async Task<int> RunOnce(int batchSize, int maxRetries, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var candidates = await _db.WebhookDeliveries
            .Where(x =>
                x.Status == WebhookDeliveryStatus.Published ||
                (x.Status == WebhookDeliveryStatus.Failed && x.NextRetryAt != null && x.NextRetryAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return 0;

        foreach (var d in candidates)
            d.MarkProcessing();

        await _db.SaveChangesAsync(ct);

        foreach (var d in candidates)
        {
            try
            {
                var subs = await _db.WebhookSubscriptions
                    .Where(s => s.IsActive && s.Provider == d.Provider)
                    .AsNoTracking()
                    .ToListAsync(ct);

                var matched = subs
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

                using var res = await http.SendAsync(req, ct);
                var resBody = await res.Content.ReadAsStringAsync(ct);

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

                if (nextRetryCount > maxRetries)
                {
                    d.MarkDead(ex.Message, httpEx?.StatusCode, httpEx?.ResponseBody);
                    continue;
                }

                var retryDelay = GetRetryDelay(nextRetryCount - 1);
                d.MarkFailed(ex.Message, retryDelay, httpEx?.StatusCode, httpEx?.ResponseBody);
            }
        }

        await _db.SaveChangesAsync(ct);
        return candidates.Count;
    }
}
