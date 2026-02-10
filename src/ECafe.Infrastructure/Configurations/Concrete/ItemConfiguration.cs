using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class ItemConfiguration : DbEntityConfig<Item>
    {
        public override void Configure(EntityTypeBuilder<Item> builder)
        {
            builder.HasKey(e => e.Id).HasName("items_pkey");

            builder.ToTable("items", "catalog");

            builder.HasIndex(e => new { e.CategoryId, e.Name }, "items_category_id_name_key").IsUnique();

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.BasePrice)
                .HasPrecision(10, 2)
                .HasColumnName("base_price");
            builder.Property(e => e.CategoryId).HasColumnName("category_id");
            builder.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            builder.Property(e => e.ImageUrl)
                .HasMaxLength(255)
                .HasColumnName("image_url");
            builder.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            builder.Property(e => e.IsAvailable)
                .HasDefaultValue(true)
                .HasColumnName("is_available");
            builder.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.SalesCount)
                .HasDefaultValue(0)
                .HasColumnName("sales_count");
            builder.Property(e => e.UnavailableReason).HasColumnName("unavailable_reason");

            builder.HasOne(d => d.Category).WithMany(p => p.Items)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("items_category_id_fkey");

            builder.HasOne(d => d.Restaurant).WithMany(p => p.Items)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("items_restaurant_id_fkey");

        }
    }
}
