using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class RolePermissionConfiguration : DbEntityConfig<RolePermission>
    {
        public override void Configure(EntityTypeBuilder<RolePermission> builder)
        {
            builder.HasKey(e => new { e.RoleId, e.PermissionId }).HasName("role_permissions_pkey");

            builder.ToTable("role_permisions", "auth");

            builder.HasIndex(e => e.RoleId, "role_permisions_role_id_key").IsUnique();

            builder.Property(e => e.RoleId).HasColumnName("role_id");
            builder.Property(e => e.PermissionId).HasColumnName("permission_id");


            builder.HasOne(d => d.Permission).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.PermissionId)
                .HasConstraintName("role_permisions_permission_id_fkey");

            builder.HasOne(d => d.Role).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("role_permisions_role_id_fkey");
        }
    }
}
