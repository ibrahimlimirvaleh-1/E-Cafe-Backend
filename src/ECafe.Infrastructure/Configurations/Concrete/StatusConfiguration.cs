using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class StatusConfiguration : DbEntityConfig<Status>
    {
        public override void Configure(EntityTypeBuilder<Status> builder)
        {

            builder.HasKey(e => e.Id).HasName("statuses_pkey");

            builder.ToTable("statuses", "auth");

            builder.HasIndex(e => new { e.StatusTypeId, e.Name }, "statuses_status_type_id_name_key").IsUnique();

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            builder.Property(e => e.StatusTypeId).HasColumnName("status_type_id");

            builder.HasOne(d => d.StatusType).WithMany(p => p.Statuses)
                .HasForeignKey(d => d.StatusTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("statuses_status_type_id_fkey");
        }
    }
}
