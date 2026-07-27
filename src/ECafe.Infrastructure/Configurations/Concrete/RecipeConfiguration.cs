using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class RecipeConfiguration : DbEntityConfig<Recipe>
    {
        public override void Configure(EntityTypeBuilder<Recipe> builder)
        {
            builder.HasKey(e => e.Id).HasName("recipes_pkey");

            builder.ToTable("recipes", "inventory");

            builder.HasIndex(e => new { e.RestaurantId, e.ItemId, e.InventoryItemId }, "recipes_restaurant_item_inventory_key")
                .IsUnique();
            builder.HasIndex(e => new { e.RestaurantId, e.ItemId }, "recipes_restaurant_id_item_id_idx");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.ItemId).HasColumnName("item_id");
            builder.Property(e => e.InventoryItemId).HasColumnName("inventory_item_id");
            builder.Property(e => e.Quantity)
                .HasPrecision(18, 6)
                .HasColumnName("quantity");
            builder.Property(e => e.UnitId).HasColumnName("unit_id");

            builder.HasOne<Restaurant>()
                .WithMany()
                .HasForeignKey(e => e.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("recipes_restaurant_id_fkey");

            builder.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("recipes_item_id_fkey");

            builder.HasOne(e => e.InventoryItem)
                .WithMany(e => e.Recipes)
                .HasForeignKey(e => e.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("recipes_inventory_item_id_fkey");

            builder.HasOne(e => e.Unit)
                .WithMany()
                .HasForeignKey(e => e.UnitId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("recipes_unit_id_fkey");
        }
    }
}
