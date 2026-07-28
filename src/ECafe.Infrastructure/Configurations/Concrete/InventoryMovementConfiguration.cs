using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class InventoryMovementConfiguration : DbEntityConfig<InventoryMovement>
    {
        public override void Configure(EntityTypeBuilder<InventoryMovement> builder)
        {
            builder.HasKey(e => e.Id).HasName("inventory_movements_pkey");

            builder.ToTable("inventory_movements", "inventory");

            builder.HasIndex(e => new { e.RestaurantId, e.InventoryItemId, e.CreatedAt }, "inventory_movements_restaurant_item_created_at_idx");
            builder.HasIndex(e => e.RelatedOrderId, "inventory_movements_related_order_id_idx");
            builder.HasIndex(e => e.MovementTypeId, "inventory_movements_movement_type_id_idx");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.InventoryItemId).HasColumnName("inventory_item_id");
            builder.Property(e => e.QuantityChange)
                .HasPrecision(18, 6)
                .HasColumnName("quantity_change");
            builder.Property(e => e.UnitId).HasColumnName("unit_id");
            builder.Property(e => e.MovementTypeId).HasColumnName("movement_type_id");
            builder.Property(e => e.Reason)
                .HasMaxLength(500)
                .HasColumnName("reason");
            builder.Property(e => e.RelatedOrderId).HasColumnName("related_order_id");

            builder.HasOne<Restaurant>()
                .WithMany()
                .HasForeignKey(e => e.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("inventory_movements_restaurant_id_fkey");

            builder.HasOne(e => e.InventoryItem)
                .WithMany(e => e.Movements)
                .HasForeignKey(e => e.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("inventory_movements_inventory_item_id_fkey");

            builder.HasOne(e => e.Unit)
                .WithMany()
                .HasForeignKey(e => e.UnitId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("inventory_movements_unit_id_fkey");

            builder.HasOne(e => e.MovementType)
                .WithMany()
                .HasForeignKey(e => e.MovementTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("inventory_movements_movement_type_id_fkey");

            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(e => e.RelatedOrderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("inventory_movements_related_order_id_fkey");
        }
    }
}
