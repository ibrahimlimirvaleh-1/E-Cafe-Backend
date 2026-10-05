using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class MobilePushDeliveryConfiguration : IEntityTypeConfiguration<MobilePushDelivery>
{
    public void Configure(EntityTypeBuilder<MobilePushDelivery> builder)
    {
        builder.ToTable("mobile_push_deliveries", "core");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OutboxEventId).HasColumnName("outbox_event_id");
        builder.Property(x => x.InstallationId).HasColumnName("installation_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.RestaurantId).HasColumnName("restaurant_id");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(x => x.LockedUntil).HasColumnName("locked_until");
        builder.Property(x => x.FirstSentAt).HasColumnName("first_sent_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.TicketId).HasMaxLength(128).HasColumnName("ticket_id");
        builder.Property(x => x.SentTokenHash).HasMaxLength(64).HasColumnName("sent_token_hash");
        builder.Property(x => x.LastErrorCode).HasMaxLength(100).HasColumnName("last_error_code");
        builder.HasIndex(x => new { x.OutboxEventId, x.InstallationId }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt, x.LockedUntil });
        builder.HasOne<OutboxEvent>()
            .WithMany()
            .HasForeignKey(x => x.OutboxEventId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MobilePushInstallation>()
            .WithMany()
            .HasForeignKey(x => x.InstallationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
