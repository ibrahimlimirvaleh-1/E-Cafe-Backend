using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
    {
        public void Configure(EntityTypeBuilder<OutboxEvent> builder)
        {
            builder.ToTable("outbox_events", "audit");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.EventType)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.AggregateType)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Payload)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(x => x.LastError)
                .HasMaxLength(2000);

            builder.HasIndex(x => new { x.ProcessedAt, x.LockedUntil, x.OccurredAt });

            builder.HasIndex(x => new { x.AggregateType, x.AggregateId });
        }
    }
}
