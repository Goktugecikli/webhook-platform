using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHooks.Api.Controllers.Webhooks;
using WebHooks.Domain;
using WebHooks.Infrastructre.Persistence;

namespace Webooks.Api.Controllers.Webhooks;

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

        _db.WebhookDeliveries.AddRange(deliveries);

        await _db.SaveChangesAsync(ct);

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
