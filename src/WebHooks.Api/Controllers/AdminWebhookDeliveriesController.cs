using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHooks.Domain;
using WebHooks.Infrastructre.Persistence;

namespace WebHooks.Api.Controllers;

[ApiController]
[Route("admin/webhook-deliveries")]
public class AdminWebhookDeliveriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminWebhookDeliveriesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] WebhookDeliveryStatus? status,
        [FromQuery] string? provider,
        [FromQuery] string? eventType,
        [FromQuery] string? tenantId,
        [FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);

        var q = _db.WebhookDeliveries.AsNoTracking();

        if (status != null) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(provider)) q = q.Where(x => x.Provider == provider);
        if (!string.IsNullOrWhiteSpace(eventType)) q = q.Where(x => x.EventType == eventType);
        if (!string.IsNullOrWhiteSpace(tenantId)) q = q.Where(x => x.TenantId == tenantId);

        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.Provider,
                x.EventType,
                x.TargetUrl,
                x.Status,
                x.RetryCount,
                x.AttemptCount,
                x.LastAttemptAt,
                x.NextRetryAt,
                x.LastStatusCode,
                x.LastError,
                x.UpdatedAt,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var x = await _db.WebhookDeliveries.AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new
            {
                d.Id,
                d.TenantId,
                d.Provider,
                d.EventType,
                d.TargetUrl,
                d.IdempotencyKey,
                d.Status,
                d.RetryCount,
                d.AttemptCount,
                d.LastAttemptAt,
                d.NextRetryAt,
                d.LastStatusCode,
                d.LastResponseSnippet,
                d.LastError,
                d.UpdatedAt,
                d.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (x is null) return NotFound();
        return Ok(x);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id)
    {
        var delivery = await _db.WebhookDeliveries.FirstOrDefaultAsync(x => x.Id == id);
        if (delivery is null) return NotFound();

        if (delivery.Status == WebhookDeliveryStatus.Succeeded)
            return BadRequest(new { error = "Delivery already succeeded" });

        delivery.MarkQueuedForManualRetry();
        await _db.SaveChangesAsync();

        return Ok(new { delivery.Id, delivery.Status });
    }
}
