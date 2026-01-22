namespace WebHooks.Api.Contracts.Events;

public sealed class PublishEventRequest
{
    public string TenantId { get; set; } = null!;
    public string Provider { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public string? IdempotencyKey { get; set; }
}