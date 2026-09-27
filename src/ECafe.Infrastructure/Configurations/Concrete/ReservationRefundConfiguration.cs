using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class ReservationRefundConfiguration : DbEntityConfig<ReservationRefund>
{
    public override void Configure(EntityTypeBuilder<ReservationRefund> builder)
    {
        builder.HasKey(e => e.Id).HasName("reservation_refunds_pkey");
        builder.ToTable("reservation_refunds", "billing");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ReservationId).HasColumnName("reservation_id");
        builder.Property(e => e.SourcePaymentProofId).HasColumnName("source_payment_proof_id");
        builder.Property(e => e.StatusId).HasColumnName("status_id");
        builder.Property(e => e.Amount).HasPrecision(18, 2).HasColumnName("amount");
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).HasColumnName("currency_code");
        builder.Property(e => e.InitiatedByUserId).HasColumnName("initiated_by_user_id");
        builder.Property(e => e.RequestedAt).HasColumnName("requested_at");
        builder.Property(e => e.ApprovedByUserId).HasColumnName("approved_by_user_id");
        builder.Property(e => e.ApprovedAt).HasColumnName("approved_at");
        builder.Property(e => e.RefundedAt).HasColumnName("refunded_at");
        builder.Property(e => e.EligibilityReason).HasMaxLength(500).HasColumnName("eligibility_reason");
        builder.Property(e => e.CancellationReasonSnapshot).HasMaxLength(500).HasColumnName("cancellation_reason_snapshot");

        builder.HasIndex(e => e.ReservationId, "reservation_refunds_reservation_id_key").IsUnique();
        builder.HasIndex(e => new { e.StatusId, e.RequestedAt }, "reservation_refunds_status_requested_at_idx");

        builder.HasOne(e => e.Reservation)
            .WithMany(e => e.Refunds)
            .HasForeignKey(e => e.ReservationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_refunds_reservation_id_fkey");

        builder.HasOne(e => e.SourcePaymentProof)
            .WithMany(e => e.SourceRefunds)
            .HasForeignKey(e => e.SourcePaymentProofId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_refunds_source_payment_proof_id_fkey");

        builder.HasOne(e => e.Status)
            .WithMany()
            .HasForeignKey(e => e.StatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_refunds_status_id_fkey");

        builder.HasOne(e => e.InitiatedByUser)
            .WithMany()
            .HasForeignKey(e => e.InitiatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("reservation_refunds_initiated_by_user_id_fkey");

        builder.HasOne(e => e.ApprovedByUser)
            .WithMany()
            .HasForeignKey(e => e.ApprovedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("reservation_refunds_approved_by_user_id_fkey");
    }
}
