using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class OrderConfiguration : DbEntityConfig<Order>
    {
        public override void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(e => e.Id).HasName("orders_pkey");

            builder.ToTable("orders", "ops");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.ClosedAt).HasColumnName("closed_at");
            builder.Property(e => e.Note).HasColumnName("note");
            builder.Property(e => e.OpenedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("opened_at");
            builder.Property(e => e.ReservationId).HasColumnName("reservation_id");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.StatusId).HasColumnName("status_id");
            builder.Property(e => e.TableId).HasColumnName("table_id");
            builder.Property(e => e.WaiterUserId).HasColumnName("waiter_user_id");

            builder.HasOne(d => d.Reservation).WithMany(p => p.Orders)
                .HasForeignKey(d => d.ReservationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_reservation_id_fkey");

            builder.HasOne(d => d.Restaurant).WithMany(p => p.Orders)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("orders_restaurant_id_fkey");

            builder.HasOne(d => d.Status).WithMany(p => p.Orders)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("orders_status_id_fkey");

            builder.HasOne(d => d.Table).WithMany(p => p.Orders)
                .HasForeignKey(d => d.TableId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("orders_table_id_fkey");

            builder.HasOne(d => d.WaiterUser).WithMany(p => p.Orders)
                .HasForeignKey(d => d.WaiterUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_waiter_user_id_fkey");
        }
    }
}
