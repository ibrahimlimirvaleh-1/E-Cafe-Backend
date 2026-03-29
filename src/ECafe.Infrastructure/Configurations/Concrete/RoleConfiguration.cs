using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class RoleConfiguration : DbEntityConfig<Role>
    {
        public override void Configure(EntityTypeBuilder<Role> builder)
        {

            builder.HasKey(e => e.Id).HasName("roles_pkey");

            builder.ToTable("roles", "auth");

            builder.HasIndex(e => e.Name, "roles_name_key").IsUnique();

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");

        }
    }
}
