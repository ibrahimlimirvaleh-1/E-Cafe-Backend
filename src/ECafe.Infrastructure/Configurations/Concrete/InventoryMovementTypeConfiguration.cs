using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class InventoryMovementTypeConfiguration : DbEntityConfig<InventoryMovementType>
    {
        public override void Configure(EntityTypeBuilder<InventoryMovementType> builder)
        {
            builder.HasKey(e => e.Id).HasName("inventory_movement_types_pkey");

            builder.ToTable("inventory_movement_types", "inventory");

            builder.HasIndex(e => e.Code, "inventory_movement_types_code_key").IsUnique();
            builder.HasIndex(e => e.Name, "inventory_movement_types_name_key").IsUnique();

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            builder.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            builder.Property(e => e.Description)
                .HasMaxLength(300)
                .HasColumnName("description");
        }
    }
}
