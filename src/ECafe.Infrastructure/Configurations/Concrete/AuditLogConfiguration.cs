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

            builder.Property(x => x.Action)
                .HasMaxLength(50);

            builder.Property(x => x.OldValues)
                .HasColumnType("text");

            builder.Property(x => x.NewValues)
                .HasColumnType("text");

            builder.Property(x => x.IpAddress)
                .HasMaxLength(50);

            builder.Property(x => x.UserAgent)
                .HasMaxLength(500);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
