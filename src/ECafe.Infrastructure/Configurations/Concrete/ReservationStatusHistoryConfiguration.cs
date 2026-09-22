using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public class ReservationStatusHistoryConfiguration : IEntityTypeConfiguration<ReservationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ReservationStatusHistory> builder)
    {
        builder.HasKey(e => e.Id).HasName("reservation_status_history_pkey");

        builder.ToTable("reservation_status_history", "ops");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ReservationId).HasColumnName("reservation_id");
        builder.Property(e => e.FromStatusId).HasColumnName("from_status_id");
        builder.Property(e => e.ToStatusId).HasColumnName("to_status_id");
        builder.Property(e => e.ChangedByUserId).HasColumnName("changed_by_user_id");
        builder.Property(e => e.ChangedAt)
            .HasColumnName("changed_at")
            .HasDefaultValueSql("now()");
        builder.Property(e => e.Reason)
            .HasMaxLength(500)
            .HasColumnName("reason");

        builder.HasIndex(e => new { e.ReservationId, e.ChangedAt }, "reservation_status_history_reservation_changed_at_idx");

        builder.HasOne(e => e.Reservation)
            .WithMany(e => e.StatusHistory)
            .HasForeignKey(e => e.ReservationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_status_history_reservation_id_fkey");

        builder.HasOne(e => e.FromStatus)
            .WithMany()
            .HasForeignKey(e => e.FromStatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_status_history_from_status_id_fkey");

        builder.HasOne(e => e.ToStatus)
            .WithMany()
            .HasForeignKey(e => e.ToStatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_status_history_to_status_id_fkey");

        builder.HasOne(e => e.ChangedByUser)
            .WithMany()
            .HasForeignKey(e => e.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("reservation_status_history_changed_by_user_id_fkey");
    }
}
