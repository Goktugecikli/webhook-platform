namespace WebHooks.Domain;

public class WebhookDelivery
{
    private WebhookDelivery() { } // EF Core

    public Guid Id { get; private set; }
    public string Provider { get; private set; } = null!;
    public string EventType { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public string IdempotencyKey { get; private set; }
    public string TenantId { get; private set; } = null!;
    public string TargetUrl { get; private set; } = null!;


    public WebhookDeliveryStatus Status { get; private set; }

    // Retry / attempts
    public int RetryCount { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }
    public DateTime? NextRetryAt { get; private set; }

    // Error / response observability
    public string? LastError { get; private set; }
    public int? LastStatusCode { get; private set; }
    public string? LastResponseSnippet { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static WebhookDelivery Create(
        string tenantId,
        string provider,
        string eventType,
        string payload,
        string idempotencyKey,
        string targetUrl)
    {
        return new WebhookDelivery
        {
            Id = Guid.NewGuid(),

            TenantId = tenantId,
            Provider = provider,
            EventType = eventType,
            Payload = payload,
            IdempotencyKey = idempotencyKey,
            TargetUrl = targetUrl,

            Status = WebhookDeliveryStatus.Received,

            RetryCount = 0,
            AttemptCount = 0,
            LastAttemptAt = null,
            NextRetryAt = null,

            LastError = null,
            LastStatusCode = null,
            LastResponseSnippet = null,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }


    public void MarkPublished()
    {
        Status = WebhookDeliveryStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Dispatch denemesi başlıyor: attempt sayısını artırır ve son deneme zamanını set eder.
    /// </summary>
    public void MarkProcessing()
    {
        Status = WebhookDeliveryStatus.Processing;
        AttemptCount++;
        LastAttemptAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkSucceeded(int? statusCode = null, string? responseSnippet = null)
    {
        Status = WebhookDeliveryStatus.Succeeded;
        LastError = null;
        NextRetryAt = null;

        LastStatusCode = statusCode;
        LastResponseSnippet = Truncate(responseSnippet);

        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error, TimeSpan retryDelay, int? statusCode = null, string? responseSnippet = null)
    {
        RetryCount++;
        LastError = error;
        Status = WebhookDeliveryStatus.Failed;
        NextRetryAt = DateTime.UtcNow.Add(retryDelay);

        LastStatusCode = statusCode;
        LastResponseSnippet = Truncate(responseSnippet);

        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDead(string error, int? statusCode = null, string? responseSnippet = null)
    {
        LastError = error;
        Status = WebhookDeliveryStatus.Dead;

        NextRetryAt = null;

        LastStatusCode = statusCode;
        LastResponseSnippet = Truncate(responseSnippet);

        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkQueuedForManualRetry()
    {
        Status = WebhookDeliveryStatus.Published;
        NextRetryAt = null;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }


    private static string? Truncate(string? s, int maxLen = 1024)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= maxLen ? s : s.Substring(0, maxLen);
    }
}
