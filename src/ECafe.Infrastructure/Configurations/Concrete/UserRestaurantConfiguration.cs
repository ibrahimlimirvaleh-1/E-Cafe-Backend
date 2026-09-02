using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class UserRestaurantConfiguration : DbEntityConfig<UserRestaurant>
    {
        public override void Configure(EntityTypeBuilder<UserRestaurant> builder)
        {
            builder.HasKey(e => new { e.UserId, e.RestaurantId }).HasName("user_restaurants_pkey");

            builder.ToTable("user_restaurants", "auth", t => t.HasCheckConstraint(
                "ck_user_restaurants_max_active_table_count_positive",
                "max_active_table_count IS NULL OR max_active_table_count > 0"));

            builder.HasIndex(e => e.RestaurantId, "user_restaurants_restaurant_id_idx");
            builder.HasIndex(e => e.RoleId, "user_restaurants_role_id_idx");

            builder.Property(e => e.UserId).HasColumnName("user_id");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.RoleId).HasColumnName("role_id");
            builder.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            builder.Property(e => e.ServiceFeePercent)
                .HasPrecision(5, 2)
                .HasColumnName("service_fee_percent")
                .IsRequired(false);

            builder.Property(e => e.MaxActiveTableCount)
                .HasColumnName("max_active_table_count")
                .IsRequired(false);

            builder.HasOne(d => d.Restaurant).WithMany(p => p.UserRestaurants)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("user_restaurants_restaurant_id_fkey");

            builder.HasOne(d => d.Role).WithMany(p => p.UserRestaurants)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("user_restaurants_role_id_fkey");

            builder.HasOne(d => d.User).WithMany(p => p.UserRestaurants)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_restaurants_user_id_fkey");
        }
    }
}
