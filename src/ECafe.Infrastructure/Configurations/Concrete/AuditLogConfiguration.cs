using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class AuditLogConfiguration
    : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("audit_logs", "audit");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.EntityName)
                .HasMaxLength(100);

            builder.Property(x => x.EntityDisplayName)
                .HasMaxLength(200);

            builder.Property(x => x.Action)
                .HasMaxLength(100);

            builder.Property(x => x.ActorFullName)
                .HasMaxLength(200);

            builder.Property(x => x.ActorRoleName)
                .HasMaxLength(100);

            builder.Property(x => x.ActorEmail)
                .HasMaxLength(256);

            builder.Property(x => x.OldValues)
                .HasColumnType("text");

            builder.Property(x => x.NewValues)
                .HasColumnType("text");

            builder.Property(x => x.Metadata)
                .HasColumnType("jsonb");

            builder.Property(x => x.CorrelationId)
                .HasMaxLength(100);

            builder.Property(x => x.IpAddress)
                .HasMaxLength(50);

            builder.Property(x => x.UserAgent)
                .HasMaxLength(500);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => x.EventId)
                .IsUnique()
                .HasFilter("\"EventId\" IS NOT NULL");

            builder.HasIndex(x => new { x.RestaurantId, x.OccurredAt });

            builder.HasIndex(x => new { x.RestaurantId, x.UserId, x.OccurredAt });

            builder.HasIndex(x => new { x.RestaurantId, x.Action, x.OccurredAt });

            builder.HasIndex(x => new { x.EntityName, x.EntityId });
        }
    }
}
