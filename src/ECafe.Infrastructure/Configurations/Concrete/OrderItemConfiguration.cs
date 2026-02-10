using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class OrderItemConfiguration : DbEntityConfig<OrderItem>
    {
        public override void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.HasKey(e => e.Id).HasName("order_items_pkey");

            builder.ToTable("order_items", "ops");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.ItemId).HasColumnName("item_id");
            builder.Property(e => e.LineTotal)
                .HasPrecision(10, 2)
                .HasColumnName("line_total");
            builder.Property(e => e.Note).HasColumnName("note");
            builder.Property(e => e.OrderId).HasColumnName("order_id");
            builder.Property(e => e.Quantity).HasColumnName("quantity");
            builder.Property(e => e.UnitPrice)
                .HasPrecision(10, 2)
                .HasColumnName("unit_price");

            builder.HasOne(d => d.Item).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("order_items_item_id_fkey");

            builder.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("order_items_order_id_fkey");

        }
    }
}
