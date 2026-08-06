using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class TableSessionConfiguration : DbEntityConfig<TableSession>
    {
        public override void Configure(EntityTypeBuilder<TableSession> builder)
        {
            builder.HasKey(e => e.Id).HasName("table_sessions_pkey");

            builder.ToTable("table_sessions", "ops");

            builder.HasIndex(e => new { e.RestaurantId, e.TableId, e.StatusId }, "table_sessions_restaurant_table_status_idx");
            builder.HasIndex(e => new { e.RestaurantId, e.TableId }, "ux_table_sessions_open_table")
                .IsUnique()
                .HasFilter("status_id = 7001 AND \"IsDeleted\" = false");
            builder.HasIndex(e => e.ReservationId, "table_sessions_reservation_id_idx");
            builder.HasIndex(e => e.WaiterUserId, "table_sessions_waiter_user_id_idx");
            builder.HasIndex(e => e.CustomerUserId, "table_sessions_customer_user_id_idx");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.TableId).HasColumnName("table_id");
            builder.Property(e => e.CustomerUserId).HasColumnName("customer_user_id");
            builder.Property(e => e.WaiterUserId).HasColumnName("waiter_user_id");
            builder.Property(e => e.ReservationId).HasColumnName("reservation_id");
            builder.Property(e => e.StatusId).HasColumnName("status_id");
            builder.Property(e => e.OpenedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("opened_at");
            builder.Property(e => e.ClosedAt).HasColumnName("closed_at");
            builder.Property(e => e.Note).HasColumnName("note");

            builder.HasOne(e => e.Restaurant)
                .WithMany(e => e.TableSessions)
                .HasForeignKey(e => e.RestaurantId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("table_sessions_restaurant_id_fkey");

            builder.HasOne(e => e.Table)
                .WithMany(e => e.TableSessions)
                .HasForeignKey(e => e.TableId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("table_sessions_table_id_fkey");

            builder.HasOne(e => e.CustomerUser)
                .WithMany()
                .HasForeignKey(e => e.CustomerUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("table_sessions_customer_user_id_fkey");

            builder.HasOne(e => e.WaiterUser)
                .WithMany()
                .HasForeignKey(e => e.WaiterUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("table_sessions_waiter_user_id_fkey");

            builder.HasOne(e => e.Reservation)
                .WithMany(e => e.TableSessions)
                .HasForeignKey(e => e.ReservationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("table_sessions_reservation_id_fkey");

            builder.HasOne(e => e.Status)
                .WithMany(e => e.TableSessions)
                .HasForeignKey(e => e.StatusId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("table_sessions_status_id_fkey");
        }
    }
}
