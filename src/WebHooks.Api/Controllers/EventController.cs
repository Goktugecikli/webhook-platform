using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHooks.Api.Contracts.Events;
using WebHooks.Domain;
using WebHooks.Infrastructre.Persistence;

namespace WebHooks.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventController : ControllerBase
{
    private readonly AppDbContext _db;

    public EventController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Publish([FromBody] PublishEventRequest req)
    {
        var tenantId = req.TenantId?.Trim();
        var provider = req.Provider?.Trim();
        var eventType = req.EventType?.Trim();

        if (string.IsNullOrWhiteSpace(tenantId)) return BadRequest("tenantId is required");
        if (string.IsNullOrWhiteSpace(provider)) return BadRequest("provider is required");
        if (string.IsNullOrWhiteSpace(eventType)) return BadRequest("eventType is required");

        // 1) Active subscriptions for tenant+provider
        var subs = await _db.WebhookSubscriptions
            .AsNoTracking()
            .Where(x => x.IsActive && x.TenantId == tenantId && x.Provider == provider)
            .ToListAsync();

        // 2) Prefix match
        var matched = subs.Where(s => Matches(eventType!, s.EventPrefix)).ToList();

        if (matched.Count == 0)
        {
            return Ok(new
            {
                created = 0,
                deliveryIds = Array.Empty<Guid>()
            });
        }

        // 3) Create one delivery per subscription (targetUrl kopyalanır)
        var idempotencyKey = string.IsNullOrWhiteSpace(req.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : req.IdempotencyKey.Trim();

        var deliveries = matched.Select(s =>
        {
            var d = WebhookDelivery.Create(
                tenantId!,
                provider!,
                eventType!,
                req.Payload ?? "",
                idempotencyKey,
                s.TargetUrl
            );

            d.MarkPublished();
            return d;
        }).ToList();

        _db.WebhookDeliveries.AddRange(deliveries);
        await _db.SaveChangesAsync();

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

        // "order.*" -> "order."
        if (prefix.EndsWith(".*", StringComparison.Ordinal))
            prefix = prefix.Substring(0, prefix.Length - 1);

        return eventType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
