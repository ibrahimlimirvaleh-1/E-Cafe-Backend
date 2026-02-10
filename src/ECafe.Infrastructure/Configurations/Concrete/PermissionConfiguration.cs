using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class PermissionConfiguration : DbEntityConfig<Permission>
    {
        public override void Configure(EntityTypeBuilder<Permission> builder)
        {

            builder.HasKey(e => e.Id).HasName("permissions_pkey");

            builder.ToTable("permissions", "auth");

            builder.HasIndex(e => e.Name, "permissions_name_key").IsUnique();

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        }
    }
}
