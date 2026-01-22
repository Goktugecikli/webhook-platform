namespace WebHooks.Api.Controllers.Webhooks;

public sealed class ReceiveWebhookRequest
{
    public string TenantId { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public string? IdempotencyKey { get; set; }
}
