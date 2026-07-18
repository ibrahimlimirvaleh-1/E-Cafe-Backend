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

            builder.ToTable("restaurants", "core");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            builder.Property(e => e.Location)
                .HasMaxLength(200)
                .HasColumnName("location");
            builder.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            builder.Property(e => e.Phone)
                .HasMaxLength(50)
                .HasColumnName("phone");
            builder.Property(e => e.Email)
                .HasMaxLength(50)
                .HasColumnName("email");
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
        }
    }
}
