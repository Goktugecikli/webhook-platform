using Microsoft.AspNetCore.Mvc;
using WebHooks.Api.Controllers.Deliveries;
using WebHooks.Domain;
using WebHooks.Infrastructre.Persistence;

namespace WebHooks.Api.Controllers;

[ApiController]
[Route("api/deliveries")]
public class WebhookDeliveriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public WebhookDeliveriesController(AppDbContext db) => _db = db;

    [HttpPost]
    [HttpPost]
    public async Task<IActionResult> CreateDelivery([FromBody] CreateDeliveryRequest req)
    {
        var tenantId = req.TenantId?.Trim();
        var provider = req.Provider?.Trim();
        var eventType = req.EventType?.Trim();
        var targetUrl = req.TargetUrl?.Trim();

        if (string.IsNullOrWhiteSpace(tenantId)) return BadRequest("tenantId is required");
        if (string.IsNullOrWhiteSpace(provider)) return BadRequest("provider is required");
        if (string.IsNullOrWhiteSpace(eventType)) return BadRequest("eventType is required");
        if (string.IsNullOrWhiteSpace(targetUrl)) return BadRequest("targetUrl is required");

        var idempotencyKey = string.IsNullOrWhiteSpace(req.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : req.IdempotencyKey.Trim();

        var delivery = WebhookDelivery.Create(
            tenantId,
            provider,
            eventType,
            req.Payload ?? "",
            idempotencyKey,
            targetUrl
        );

        delivery.MarkPublished();

        _db.WebhookDeliveries.Add(delivery);
        await _db.SaveChangesAsync();

        return Ok(new { delivery.Id });
    }


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var delivery = await _db.WebhookDeliveries.FindAsync(id);
        if (delivery is null) return NotFound();

        return Ok(new
        {
            delivery.Id,
            delivery.Provider,
            delivery.EventType,
            delivery.Status,

            delivery.AttemptCount,
            delivery.LastAttemptAt,

            delivery.RetryCount,
            delivery.NextRetryAt,

            delivery.LastStatusCode,
            delivery.LastResponseSnippet,
            delivery.LastError,

            delivery.CreatedAt,
            delivery.UpdatedAt
        });
    }


}
