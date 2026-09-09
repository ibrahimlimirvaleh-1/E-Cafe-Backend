using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public class RestaurantWorkingHourConfiguration : DbEntityConfig<RestaurantWorkingHour>
{
    public override void Configure(EntityTypeBuilder<RestaurantWorkingHour> builder)
    {
        builder.HasKey(e => e.Id).HasName("restaurant_working_hours_pkey");

        builder.ToTable("restaurant_working_hours", "core", t =>
        {
            t.HasCheckConstraint(
                "ck_restaurant_working_hours_day_of_week_range",
                "day_of_week >= 0 AND day_of_week <= 6");
            t.HasCheckConstraint(
                "ck_restaurant_working_hours_open_closed_not_equal",
                "is_closed = true OR opens_at <> closes_at");
        });

        builder.HasIndex(e => new { e.RestaurantId, e.DayOfWeek }, "ux_restaurant_working_hours_restaurant_day")
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
        builder.Property(e => e.DayOfWeek)
            .HasConversion<int>()
            .HasColumnName("day_of_week");
        builder.Property(e => e.OpensAt)
            .HasColumnType("time without time zone")
            .HasColumnName("opens_at");
        builder.Property(e => e.ClosesAt)
            .HasColumnType("time without time zone")
            .HasColumnName("closes_at");
        builder.Property(e => e.IsClosed)
            .HasDefaultValue(false)
            .HasColumnName("is_closed");

        builder.HasOne(e => e.Restaurant)
            .WithMany(e => e.WorkingHours)
            .HasForeignKey(e => e.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("restaurant_working_hours_restaurant_id_fkey");
    }
}
