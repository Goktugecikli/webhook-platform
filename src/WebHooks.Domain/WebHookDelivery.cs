using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebHooks.Domain;

public class WebhookDelivery
{
    // EF Core için
    private WebhookDelivery() { }

    public Guid Id { get; private set; }
    public string Provider { get; private set; } = null!;
    public string EventType { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;

    public WebhookDeliveryStatus Status { get; private set; }

    public int RetryCount { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public string? LastError { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Factory method (DOĞRU domain yaklaşımı)
    public static WebhookDelivery Create(
        string provider,
        string eventType,
        string payload,
        string idempotencyKey)
    {
        return new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            EventType = eventType,
            Payload = payload,
            IdempotencyKey = idempotencyKey,
            Status = WebhookDeliveryStatus.Received,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // === Domain behavior ===

    public void MarkPublished()
    {
        Status = WebhookDeliveryStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkProcessing()
    {
        Status = WebhookDeliveryStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkSucceeded()
    {
        Status = WebhookDeliveryStatus.Succeeded;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error, TimeSpan retryDelay)
    {
        RetryCount++;
        LastError = error;
        Status = WebhookDeliveryStatus.Failed;
        NextRetryAt = DateTime.UtcNow.Add(retryDelay);
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDead(string error)
    {
        LastError = error;
        Status = WebhookDeliveryStatus.Dead;
        UpdatedAt = DateTime.UtcNow;
    }
}

