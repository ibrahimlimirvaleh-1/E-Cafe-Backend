using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class ReservationRefundStatusHistoryConfiguration : DbEntityConfig<ReservationRefundStatusHistory>
{
    public override void Configure(EntityTypeBuilder<ReservationRefundStatusHistory> builder)
    {
        builder.HasKey(e => e.Id).HasName("reservation_refund_status_histories_pkey");
        builder.ToTable("reservation_refund_status_histories", "billing");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ReservationRefundId).HasColumnName("reservation_refund_id");
        builder.Property(e => e.FromStatusId).HasColumnName("from_status_id");
        builder.Property(e => e.ToStatusId).HasColumnName("to_status_id");
        builder.Property(e => e.ChangedByUserId).HasColumnName("changed_by_user_id");
        builder.Property(e => e.ChangedAt).HasColumnName("changed_at");
        builder.Property(e => e.Reason).HasMaxLength(500).HasColumnName("reason");

        builder.HasIndex(e => new { e.ReservationRefundId, e.ChangedAt }, "reservation_refund_status_histories_refund_changed_at_idx");

        builder.HasOne(e => e.ReservationRefund)
            .WithMany(e => e.StatusHistory)
            .HasForeignKey(e => e.ReservationRefundId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_refund_status_histories_refund_id_fkey");

        builder.HasOne(e => e.FromStatus)
            .WithMany()
            .HasForeignKey(e => e.FromStatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_refund_status_histories_from_status_id_fkey");

        builder.HasOne(e => e.ToStatus)
            .WithMany()
            .HasForeignKey(e => e.ToStatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_refund_status_histories_to_status_id_fkey");

        builder.HasOne(e => e.ChangedByUser)
            .WithMany()
            .HasForeignKey(e => e.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("reservation_refund_status_histories_changed_by_user_id_fkey");
    }
}
