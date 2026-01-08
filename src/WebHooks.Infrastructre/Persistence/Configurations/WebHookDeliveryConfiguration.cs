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

        b.Property(x => x.Provider)
            .HasMaxLength(100)
            .IsRequired();

        b.Property(x => x.EventType)
            .HasMaxLength(200)
            .IsRequired();

        b.Property(x => x.Payload)
            .IsRequired();

        b.Property(x => x.IdempotencyKey)
            .HasMaxLength(200)
            .IsRequired();

        b.HasIndex(x => new { x.Provider, x.IdempotencyKey })
            .IsUnique();

        b.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.RetryCount).IsRequired();

        b.Property(x => x.NextRetryAt);

        b.Property(x => x.LastError)
            .HasMaxLength(2000);

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
    }
}
