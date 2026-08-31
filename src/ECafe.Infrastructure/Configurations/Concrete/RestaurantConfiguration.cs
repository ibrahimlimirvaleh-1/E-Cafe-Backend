using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class RestaurantConfiguration : DbEntityConfig<Restaurant>
    {
        public override void Configure(EntityTypeBuilder<Restaurant> builder)
        {
            builder.HasKey(e => e.Id).HasName("restaurants_pkey");

            builder.ToTable("restaurants", "core", t =>
            {
                t.HasCheckConstraint(
                    "ck_restaurants_latitude_range",
                    "latitude IS NULL OR (latitude >= -90 AND latitude <= 90)");
                t.HasCheckConstraint(
                    "ck_restaurants_longitude_range",
                    "longitude IS NULL OR (longitude >= -180 AND longitude <= 180)");
            });

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            builder.Property(e => e.Location)
                .HasMaxLength(200)
                .HasColumnName("location");
            builder.Property(e => e.Latitude)
                .HasColumnName("latitude");
            builder.Property(e => e.Longitude)
                .HasColumnName("longitude");
            builder.Property(e => e.PlaceId)
                .HasMaxLength(150)
                .HasColumnName("place_id");
            builder.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            builder.Property(e => e.Phone)
                .HasMaxLength(50)
                .HasColumnName("phone");
            builder.Property(e => e.Email)
                .HasMaxLength(50)
                .HasColumnName("email");
            builder.Property(e => e.RestaurantGroupId)
                .HasColumnName("restaurant_group_id");
            builder.Property(e => e.BranchName)
                .HasMaxLength(100)
                .HasColumnName("branch_name");
            builder.Property(e => e.RatingAverage)
                .HasPrecision(3, 2)
                .HasColumnName("rating_average");
            builder.Property(e => e.RatingCount)
                .HasDefaultValue(0)
                .HasColumnName("rating_count");
            builder.Property(e => e.DepositAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("deposit_amount");
            builder.Property(e => e.CancellationWindowMinutes)
                .HasDefaultValue(60)
                .HasColumnName("cancellation_window_minutes");
            builder.Property(e => e.ServiceFeePercent)
                .HasPrecision(5, 2)
                .HasDefaultValue(0m)
                .HasColumnName("service_fee_percent");

            builder.Property(e => e.StaffSettlementPeriod)
                .HasDefaultValue((int)StaffSettlementPeriod.Weekly)
                .HasColumnName("staff_settlement_period");
            builder.HasIndex(e => e.Email).HasDatabaseName("restaurants_email_key").IsUnique();

            builder.HasOne(e => e.RestaurantGroup)
                .WithMany(e => e.Restaurants)
                .HasForeignKey(e => e.RestaurantGroupId)
                .HasConstraintName("restaurants_restaurant_group_id_fkey");
        }
    }
}
