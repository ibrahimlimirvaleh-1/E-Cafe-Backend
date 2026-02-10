using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class CategoryConfiguration : DbEntityConfig<Category>
    {
        public override void Configure(EntityTypeBuilder<Category> builder)
        {

                builder.HasKey(e => e.Id).HasName("categories_pkey");

                builder.ToTable("categories", "catalog");

                builder.HasIndex(e => new { e.RestaurantId, e.Name }, "categories_restaurant_id_name_key").IsUnique();

                builder.HasIndex(e => new { e.RestaurantId, e.Slug }, "categories_restaurant_id_slug_key").IsUnique();

                builder.Property(e => e.Id).HasColumnName("id");
                builder.Property(e => e.IsActive)
                    .HasDefaultValue(true)
                    .HasColumnName("is_active");
                builder.Property(e => e.Name)
                    .HasMaxLength(100)
                    .HasColumnName("name");
                builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
                builder.Property(e => e.Slug)
                    .HasMaxLength(100)
                    .HasColumnName("slug");
                builder.Property(e => e.SortOrder)
                    .HasDefaultValue(0)
                    .HasColumnName("sort_order");

                builder.HasOne(d => d.Restaurant).WithMany(p => p.Categories)
                    .HasForeignKey(d => d.RestaurantId)
                    .HasConstraintName("categories_restaurant_id_fkey");
        }
    }
}
