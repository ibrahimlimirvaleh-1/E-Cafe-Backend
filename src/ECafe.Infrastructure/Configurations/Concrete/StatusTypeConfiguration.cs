using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class StatusTypeConfiguration : DbEntityConfig<StatusType>
    {
        public override void Configure(EntityTypeBuilder<StatusType> builder)
        {
            builder.HasKey(e => e.Id).HasName("status_types_pkey");

            builder.ToTable("status_types", "auth");

            builder.HasIndex(e => e.Name, "status_types_name_key").IsUnique();

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
        }
    }
}
