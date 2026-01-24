using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHooks.Domain;
using WebHooks.Infrastructre.Persistence;

namespace WebHooks.Api.Controllers.Webhooks;

[ApiController]
[Route("webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly AppDbContext _db;

    public WebhooksController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("{provider}")]
    public async Task<IActionResult> Receive(string provider, [FromBody] ReceiveWebhookRequest req, CancellationToken ct)
    {
        provider = provider?.Trim() ?? "";
        var tenantId = req.TenantId?.Trim();

        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest("provider is required");

        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest("tenantId is required");

        if (string.IsNullOrWhiteSpace(req.EventType))
            return BadRequest("eventType is required");

        if (string.IsNullOrWhiteSpace(req.Payload))
            return BadRequest("payload is required");

        // Idempotency key (header öncelikli)
        var headerKey = Request.Headers.TryGetValue("Idempotency-Key", out var h) ? h.ToString() : null;
        var idempotencyKey = !string.IsNullOrWhiteSpace(headerKey)
            ? headerKey!
            : (string.IsNullOrWhiteSpace(req.IdempotencyKey) ? Guid.NewGuid().ToString("N") : req.IdempotencyKey!.Trim());

        // tenant+provider için aktif subscription’ları çek
        var subs = await _db.WebhookSubscriptions
            .AsNoTracking()
            .Where(x => x.IsActive && x.TenantId == tenantId && x.Provider == provider)
            .ToListAsync(ct);

        var matched = subs.Where(s => Matches(req.EventType!, s.EventPrefix)).ToList();

        if (matched.Count == 0)
            return Ok(new { created = 0, deliveryIds = Array.Empty<Guid>() });

        var deliveries = matched.Select(s =>
        {
            var d = WebhookDelivery.Create(
                tenantId!,
                provider,
                req.EventType!,
                req.Payload!,
                idempotencyKey,
                s.TargetUrl
            );
            d.MarkPublished();
            return d;
        }).ToList();

        try
        {
            _db.WebhookDeliveries.AddRange(deliveries);
            await _db.SaveChangesAsync(ct);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
        {
            // Duplicate -> mevcut delivery'leri dön
            var targetUrls = matched.Select(m => m.TargetUrl).ToList();

            var existingIds = await _db.WebhookDeliveries
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.Provider == provider &&
                    x.IdempotencyKey == idempotencyKey &&
                    targetUrls.Contains(x.TargetUrl)
                )
                .Select(x => x.Id)
                .ToListAsync(ct);

            return Ok(new { created = 0, deliveryIds = existingIds.ToArray(), duplicate = true });
        }

        return Ok(new
        {
            created = deliveries.Count,
            deliveryIds = deliveries.Select(x => x.Id).ToArray()
        });

    }

    private static bool Matches(string eventType, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return false;

        prefix = prefix.Trim();
        if (prefix == "*") return true;

        if (prefix.EndsWith(".*", StringComparison.Ordinal))
            prefix = prefix.Substring(0, prefix.Length - 1);

        return eventType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

}
