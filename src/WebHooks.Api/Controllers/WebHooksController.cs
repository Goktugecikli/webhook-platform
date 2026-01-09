using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public async Task<IActionResult> Receive(string provider, [FromBody] CreateWebhookRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest("provider is required");

        if (string.IsNullOrWhiteSpace(req.EventType))
            return BadRequest("eventType is required");

        if (string.IsNullOrWhiteSpace(req.Payload))
            return BadRequest("payload is required");

        // Idempotency key: header varsa onu tercih edelim (gerçek sistemler böyle)
        var headerKey = Request.Headers.TryGetValue("Idempotency-Key", out var h) ? h.ToString() : null;
        var idempotencyKey = !string.IsNullOrWhiteSpace(headerKey)
            ? headerKey!
            : (string.IsNullOrWhiteSpace(req.IdempotencyKey) ? Guid.NewGuid().ToString("N") : req.IdempotencyKey!);

        // Duplicate kontrol (DB unique index zaten var, ama 409 için ön kontrol yapıyoruz)
        var exists = await _db.WebhookDeliveries
            .AsNoTracking()
            .AnyAsync(x => x.Provider == provider && x.IdempotencyKey == idempotencyKey, ct);

        if (exists)
            return Conflict(new { message = "Duplicate webhook (idempotency key already used)", provider, idempotencyKey });

        var delivery = WebhookDelivery.Create(
            provider: provider,
            eventType: req.EventType,
            payload: req.Payload,
            idempotencyKey: idempotencyKey
        );

        _db.WebhookDeliveries.Add(delivery);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Race condition: iki istek aynı anda geldiyse unique index patlayabilir → yine 409
            return Conflict(new { message = "Duplicate webhook (race)", provider, idempotencyKey });
        }

        return Created($"/webhooks/{provider}/{delivery.Id}", new
        {
            id = delivery.Id,
            provider,
            idempotencyKey,
            status = delivery.Status.ToString(),
            createdAt = delivery.CreatedAt
        });
    }
}
