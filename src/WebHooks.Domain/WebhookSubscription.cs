namespace WebHooks.Domain;

public class WebhookSubscription
{
    private WebhookSubscription() { } // EF

    public Guid Id { get; private set; }

    public string TenantId { get; private set; } = null!;
    public string Provider { get; private set; } = null!;

    /// <summary>
    /// Prefix match: "order.", "order.created", "*" (global)
    /// </summary>
    public string EventPrefix { get; private set; } = null!;

    public string TargetUrl { get; private set; } = null!;
    public string Secret { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static WebhookSubscription Create(
        string tenantId,
        string provider,
        string eventPrefix,
        string targetUrl,
        string secret)
    {
        // burada çok basic validation; sonra genişletiriz
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("tenantId required");
        if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("provider required");
        if (string.IsNullOrWhiteSpace(eventPrefix)) throw new ArgumentException("eventPrefix required");
        if (string.IsNullOrWhiteSpace(targetUrl)) throw new ArgumentException("targetUrl required");
        if (string.IsNullOrWhiteSpace(secret)) throw new ArgumentException("secret required");

        return new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Trim(),
            Provider = provider.Trim(),
            EventPrefix = eventPrefix.Trim(),
            TargetUrl = targetUrl.Trim(),
            Secret = secret, // şimdilik plain; ileride encryption/KeyVault
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Disable()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Enable()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
