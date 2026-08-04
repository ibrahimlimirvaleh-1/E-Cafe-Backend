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
            builder.Property(e => e.AcceptedAt).HasColumnName("accepted_at");
            builder.Property(e => e.ClosedAt).HasColumnName("closed_at");
            builder.Property(e => e.CustomerUserId).HasColumnName("customer_user_id");
            builder.Property(e => e.Note).HasColumnName("note");
            builder.Property(e => e.OpenedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("opened_at");
            builder.Property(e => e.PreparingAt).HasColumnName("preparing_at");
            builder.Property(e => e.ReadyAt).HasColumnName("ready_at");
            builder.Property(e => e.ReservationId).HasColumnName("reservation_id");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.ScheduledKitchenTime).HasColumnName("scheduled_kitchen_time");
            builder.Property(e => e.SentToKitchenAt).HasColumnName("sent_to_kitchen_at");
            builder.Property(e => e.ServedAt).HasColumnName("served_at");
            builder.Property(e => e.SourceId)
                .HasDefaultValue(1)
                .HasColumnName("source_id");
            builder.Property(e => e.StatusId).HasColumnName("status_id");
            builder.Property(e => e.TableId).HasColumnName("table_id");
            builder.Property(e => e.TableSessionId).HasColumnName("table_session_id");
            builder.Property(e => e.WaiterUserId).HasColumnName("waiter_user_id");

            builder.HasIndex(e => new { e.RestaurantId, e.StatusId, e.ScheduledKitchenTime }, "orders_restaurant_status_scheduled_kitchen_time_idx");
            builder.HasIndex(e => e.TableSessionId, "orders_table_session_id_idx");

            builder.HasOne(d => d.CustomerUser).WithMany()
                .HasForeignKey(d => d.CustomerUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_customer_user_id_fkey");

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

            builder.HasOne(d => d.TableSession).WithMany(p => p.Orders)
                .HasForeignKey(d => d.TableSessionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_table_session_id_fkey");

            builder.HasOne(d => d.WaiterUser).WithMany(p => p.Orders)
                .HasForeignKey(d => d.WaiterUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_waiter_user_id_fkey");
        }
    }
}
