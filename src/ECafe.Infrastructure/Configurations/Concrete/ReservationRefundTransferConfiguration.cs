using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class ReservationRefundTransferConfiguration : DbEntityConfig<ReservationRefundTransfer>
{
    public override void Configure(EntityTypeBuilder<ReservationRefundTransfer> builder)
    {
        builder.HasKey(e => e.Id).HasName("reservation_refund_transfers_pkey");
        builder.ToTable("reservation_refund_transfers", "billing");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ReservationRefundId).HasColumnName("reservation_refund_id");
        builder.Property(e => e.Amount).HasPrecision(18, 2).HasColumnName("amount");
        builder.Property(e => e.TransferReference).HasMaxLength(100).HasColumnName("transfer_reference");
        builder.Property(e => e.ProofFileId).HasColumnName("proof_file_id");
        builder.Property(e => e.SubmittedByUserId).HasColumnName("submitted_by_user_id");
        builder.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(e => e.CustomerConfirmedAt).HasColumnName("customer_confirmed_at");
        builder.Property(e => e.DisputedAt).HasColumnName("disputed_at");
        builder.Property(e => e.DisputeReason).HasMaxLength(500).HasColumnName("dispute_reason");

        builder.HasIndex(e => new { e.ReservationRefundId, e.SubmittedAt }, "reservation_refund_transfers_refund_submitted_at_idx");
        builder.HasIndex(e => e.ProofFileId, "reservation_refund_transfers_proof_file_id_idx");

        builder.HasOne(e => e.ReservationRefund)
            .WithMany(e => e.TransferAttempts)
            .HasForeignKey(e => e.ReservationRefundId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_refund_transfers_refund_id_fkey");

        builder.HasOne(e => e.ProofFile)
            .WithMany(e => e.ReservationRefundTransferProofs)
            .HasForeignKey(e => e.ProofFileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_refund_transfers_proof_file_id_fkey");

        builder.HasOne(e => e.SubmittedByUser)
            .WithMany()
            .HasForeignKey(e => e.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_refund_transfers_submitted_by_user_id_fkey");
    }
}
