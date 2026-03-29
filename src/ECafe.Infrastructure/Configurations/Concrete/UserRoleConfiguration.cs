using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    internal class UserRoleConfiguration : DbEntityConfig<UserRole>
    {
        public override void Configure(EntityTypeBuilder<UserRole> builder)
        {
            builder.HasKey(e => new { e.UserId, e.RoleId }).HasName("user_roles_pkey");

            builder.ToTable("user_roles", "auth");

            builder.HasIndex(e => e.UserId, "user_roles_user_id_key").IsUnique();

            builder.Property(e => e.UserId).HasColumnName("user_id");
            builder.Property(e => e.RoleId).HasColumnName("role_id");

            builder.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("user_roles_role_id_fkey");

            builder.HasOne(d => d.User).WithMany(p => p.UserRoles)
                 .HasForeignKey(d => d.UserId)
                 .HasConstraintName("user_roles_user_id_fkey");
        }
    }
}
