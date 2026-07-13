using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class ReservationConfiguration : DbEntityConfig<Reservation>
    {
        public override void Configure(EntityTypeBuilder<Reservation> builder)
        {
            builder.HasKey(e => e.Id).HasName("reservations_pkey");

            builder.ToTable("reservations", "ops");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.CompletedAt).HasColumnName("completed_at");
            builder.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
            builder.Property(e => e.CancellationDeadline).HasColumnName("cancellation_deadline");
            builder.Property(e => e.CancellationWindowMinutes).HasColumnName("cancellation_window_minutes");
            builder.Property(e => e.CustomerUserId).HasColumnName("customer_user_id");
            builder.Property(e => e.DepositAmount)
                .HasPrecision(18, 2)
                .HasColumnName("deposit_amount");
            builder.Property(e => e.Note).HasColumnName("note");
            builder.Property(e => e.NoShowAt).HasColumnName("no_show_at");
            builder.Property(e => e.PaidAt).HasColumnName("paid_at");
            builder.Property(e => e.PeopleCount).HasColumnName("people_count");
            builder.Property(e => e.RefundEligible).HasColumnName("refund_eligible");
            builder.Property(e => e.ReservedFrom).HasColumnName("reserved_from");
            builder.Property(e => e.ReservedTo).HasColumnName("reserved_to");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.SeatedAt).HasColumnName("seated_at");
            builder.Property(e => e.StatusId).HasColumnName("status_id");
            builder.Property(e => e.TableId).HasColumnName("table_id");
            builder.Property(e => e.WaiterUserId).HasColumnName("waiter_user_id");

            builder.HasOne(d => d.CustomerUser).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.CustomerUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("reservations_customer_user_id_fkey");

            builder.HasOne(d => d.Restaurant).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("reservations_restaurant_id_fkey");

            builder.HasOne(d => d.Status).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("reservations_status_id_fkey");

            builder.HasOne(d => d.Table).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.TableId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("reservations_table_id_fkey");

            builder.HasOne(d => d.WaiterUser).WithMany()
                .HasForeignKey(d => d.WaiterUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("reservations_waiter_user_id_fkey");
        }
    }
}
