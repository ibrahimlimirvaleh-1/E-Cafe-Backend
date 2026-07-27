using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class InventoryItemConfiguration : DbEntityConfig<InventoryItem>
    {
        public override void Configure(EntityTypeBuilder<InventoryItem> builder)
        {
            builder.HasKey(e => e.Id).HasName("inventory_items_pkey");

            builder.ToTable("inventory_items", "inventory");

            builder.HasIndex(e => new { e.RestaurantId, e.Name }, "inventory_items_restaurant_id_name_key")
                .IsUnique();
            builder.HasIndex(e => new { e.RestaurantId, e.IsActive }, "inventory_items_restaurant_id_is_active_idx");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            builder.Property(e => e.UnitId).HasColumnName("unit_id");
            builder.Property(e => e.QuantityOnHand)
                .HasPrecision(18, 6)
                .HasDefaultValue(0m)
                .HasColumnName("quantity_on_hand");
            builder.Property(e => e.LowStockThreshold)
                .HasPrecision(18, 6)
                .HasDefaultValue(0m)
                .HasColumnName("low_stock_threshold");
            builder.Property(e => e.LastLowStockNotifiedAt).HasColumnName("last_low_stock_notified_at");
            builder.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            builder.HasOne(e => e.Restaurant)
                .WithMany()
                .HasForeignKey(e => e.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("inventory_items_restaurant_id_fkey");

            builder.HasOne(e => e.Unit)
                .WithMany()
                .HasForeignKey(e => e.UnitId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("inventory_items_unit_id_fkey");
        }
    }
}
