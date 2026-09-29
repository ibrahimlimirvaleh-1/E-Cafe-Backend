using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public class RestaurantDepositRuleConfiguration : DbEntityConfig<RestaurantDepositRule>
{
    public override void Configure(EntityTypeBuilder<RestaurantDepositRule> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("restaurant_deposit_rules", "core", t =>
            t.HasCheckConstraint("ck_restaurant_deposit_rules_amount_positive", "amount > 0"));
        builder.HasIndex(x => new { x.RestaurantId, x.ReservationDate })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RestaurantId).HasColumnName("restaurant_id");
        builder.Property(x => x.ReservationDate).HasColumnName("reservation_date").HasColumnType("date");
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.HasOne(x => x.Restaurant)
            .WithMany(x => x.DepositRules)
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
