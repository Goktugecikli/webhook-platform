using Microsoft.AspNetCore.Mvc;
using WebHooks.Domain;
using WebHooks.Infrastructre.Persistence;
using Webooks.Api.Controllers.Webhooks;

namespace WebHooks.Api.Controllers;

[ApiController]
[Route("api/deliveries")]
public class WebhookDeliveriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public WebhookDeliveriesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> CreateDelivery(CreateWebhookRequest req)
    {
        var delivery = WebhookDelivery.Create(
            req.Provider,
            req.EventType,
            req.Payload,
            req.IdempotencyKey
        );

        // 🔑 API burada "published" eder
        delivery.MarkPublished();

        _db.WebhookDeliveries.Add(delivery);
        await _db.SaveChangesAsync();

        return Ok(new { delivery.Id });
    }
}
