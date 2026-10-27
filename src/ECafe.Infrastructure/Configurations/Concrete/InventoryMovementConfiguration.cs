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

            builder.ToTable("inventory_movements", "inventory", table => table.HasCheckConstraint("inventory_movement_reversal_valid_ck",
                "reversed_movement_id IS NULL OR (movement_type_id = 6 AND quantity_change > 0 AND related_order_id IS NOT NULL AND related_order_item_id IS NOT NULL)"));

            builder.HasIndex(e => new { e.RestaurantId, e.InventoryItemId, e.CreatedAt }, "inventory_movements_restaurant_item_created_at_idx");
            builder.HasIndex(e => e.RelatedOrderId, "inventory_movements_related_order_id_idx");
            builder.HasIndex(e => e.RelatedOrderItemId, "inventory_movements_related_order_item_id_idx");
            builder.HasIndex(e => e.MovementTypeId, "inventory_movements_movement_type_id_idx");
            builder.HasIndex(e => new { e.RelatedOrderItemId, e.InventoryItemId }, "inventory_order_consumption_once_idx")
                .IsUnique().HasFilter("movement_type_id = 4 AND related_order_item_id IS NOT NULL");
            builder.HasIndex(e => e.ReversedMovementId, "inventory_movement_reversal_once_idx")
                .IsUnique().HasFilter("reversed_movement_id IS NOT NULL");

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
            builder.Property(e => e.RelatedOrderItemId).HasColumnName("related_order_item_id");
            builder.Property(e => e.ReversedMovementId).HasColumnName("reversed_movement_id");
            builder.HasOne<InventoryMovement>().WithMany().HasForeignKey(e => e.ReversedMovementId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("inventory_movement_reversal_source_fkey");

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

            builder.HasOne(e => e.RelatedOrderItem)
                .WithMany()
                .HasForeignKey(e => e.RelatedOrderItemId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("inventory_movements_related_order_item_id_fkey");
        }
    }
}
