using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class TableConfiguration : DbEntityConfig<Table>
    {
        public override void Configure(EntityTypeBuilder<Table> builder)
        {

            builder.HasKey(e => e.Id).HasName("tables_pkey");

            builder.ToTable("tables", "ops");

            builder.HasIndex(e => new { e.RestaurantId, e.TableNo }, "tables_restaurant_id_table_no_key").IsUnique();

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Capacity).HasColumnName("capacity");
            builder.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            builder.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.TableNo).HasColumnName("table_no");

            builder.HasOne(d => d.Restaurant).WithMany(p => p.Tables)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("tables_restaurant_id_fkey");
        }
    }
}
