namespace Webooks.Api.Contracts.Subscriptions;

public sealed class CreateSubscriptionRequest
{
    public string TenantId { get; set; } = null!;
    public string Provider { get; set; } = null!;
    public string EventPrefix { get; set; } = null!;
    public string TargetUrl { get; set; } = null!;
    public string Secret { get; set; } = null!;
}