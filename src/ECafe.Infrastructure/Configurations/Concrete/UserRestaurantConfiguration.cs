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

            builder.ToTable("user_restaurants", "auth");

            builder.HasIndex(e => e.UserId, "user_restaurants_user_id_key").IsUnique();

            builder.Property(e => e.UserId).HasColumnName("user_id");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            builder.HasOne(d => d.Restaurant).WithMany(p => p.UserRestaurants)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("user_restaurants_restaurant_id_fkey");

            builder.HasOne(d => d.User).WithOne(p => p.UserRestaurant)
                .HasForeignKey<UserRestaurant>(d => d.UserId)
                .HasConstraintName("user_restaurants_user_id_fkey");
        }
    }
}
