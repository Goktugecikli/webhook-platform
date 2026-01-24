using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebHooks.Domain;

namespace WebHooks.Infrastructre.Persistence.Configurations;

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> b)
    {
        b.ToTable("webhook_deliveries");
        b.HasKey(x => x.Id);

        b.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(100).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        b.Property(x => x.Payload).HasColumnType("text").IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.TargetUrl).HasMaxLength(2048).IsRequired();

        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.RetryCount).IsRequired();
        b.Property(x => x.AttemptCount).IsRequired();
        b.Property(x => x.LastAttemptAt);
        b.Property(x => x.NextRetryAt);

        b.Property(x => x.LastError).HasMaxLength(2000);
        b.Property(x => x.LastStatusCode);
        b.Property(x => x.LastResponseSnippet).HasMaxLength(1024);

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();

        // ✅ idempotency
        b.HasIndex(x => new { x.TenantId, x.Provider, x.IdempotencyKey, x.TargetUrl })
            .IsUnique();

        // ✅ worker performans
        b.HasIndex(x => new { x.Status, x.NextRetryAt });
    }

}
