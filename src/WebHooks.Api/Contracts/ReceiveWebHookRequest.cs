namespace Webooks.Api.Controllers.Webhooks;

public sealed record CreateWebhookRequest(
    string EventType,
    string Payload,
    string? IdempotencyKey
);
