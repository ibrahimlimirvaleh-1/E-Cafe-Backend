using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class RestaurantGroupConfiguration : DbEntityConfig<RestaurantGroup>
    {
        public override void Configure(EntityTypeBuilder<RestaurantGroup> builder)
        {
            builder.HasKey(e => e.Id).HasName("restaurant_groups_pkey");

            builder.ToTable("restaurant_groups", "core");

            builder.Property(e => e.Id).HasColumnName("id");

            builder.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");

            builder.Property(e => e.LegalName)
                .HasMaxLength(200)
                .HasColumnName("legal_name");

            builder.Property(e => e.OwnerUserId)
                .HasColumnName("owner_user_id");

            builder.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            builder.HasIndex(e => e.Name)
                .HasDatabaseName("restaurant_groups_name_key")
                .IsUnique();

            builder.HasOne(e => e.OwnerUser)
                .WithMany(e => e.OwnedRestaurantGroups)
                .HasForeignKey(e => e.OwnerUserId)
                .HasConstraintName("restaurant_groups_owner_user_id_fkey");
        }
    }
}
