using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHooks.Domain;
using WebHooks.Infrastructre.Persistence;
using Webooks.Api.Contracts.Subscriptions;

namespace WebHooks.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class WebhookSubscriptionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public WebhookSubscriptionsController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionRequest req)
    {
        var sub = WebhookSubscription.Create(
            req.TenantId,
            req.Provider,
            NormalizePrefix(req.EventPrefix),
            req.TargetUrl,
            req.Secret
        );

        _db.WebhookSubscriptions.Add(sub);
        await _db.SaveChangesAsync();

        return Ok(new { sub.Id });
    }

    // Listeleme: tenantId + provider filtreleri (opsiyonel)
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? tenantId, [FromQuery] string? provider)
    {
        var q = _db.WebhookSubscriptions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(tenantId))
            q = q.Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(provider))
            q = q.Where(x => x.Provider == provider);

        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.Provider,
                x.EventPrefix,
                x.TargetUrl,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id)
    {
        var sub = await _db.WebhookSubscriptions.FindAsync(id);
        if (sub is null) return NotFound();

        sub.Disable();
        await _db.SaveChangesAsync();

        return Ok();
    }

    private static string NormalizePrefix(string prefix)
    {
        prefix = prefix.Trim();
        if (prefix == "*") return "*";

        // İstersen: "order.*" yazanları "order." normalize edelim
        if (prefix.EndsWith(".*", StringComparison.Ordinal))
            prefix = prefix.Substring(0, prefix.Length - 1); // "order."

        return prefix;
    }
}
