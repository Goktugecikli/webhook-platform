public class CreateWebhookRequest
{
    public string Provider { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public string IdempotencyKey { get; set; } = null!;
}
