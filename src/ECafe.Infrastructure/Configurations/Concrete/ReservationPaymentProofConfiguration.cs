using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public class ReservationPaymentProofConfiguration : DbEntityConfig<ReservationPaymentProof>
{
    public override void Configure(EntityTypeBuilder<ReservationPaymentProof> builder)
    {
        builder.HasKey(e => e.Id).HasName("reservation_payment_proofs_pkey");

        builder.ToTable("reservation_payment_proofs", "billing");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ReservationId).HasColumnName("reservation_id");
        builder.Property(e => e.FileId).HasColumnName("file_id");
        builder.Property(e => e.Amount)
            .HasPrecision(18, 2)
            .HasColumnName("amount");
        builder.Property(e => e.StatusId).HasColumnName("status_id");
        builder.Property(e => e.SubmittedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("submitted_at");
        builder.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(e => e.RejectReason)
            .HasMaxLength(500)
            .HasColumnName("reject_reason");

        builder.HasIndex(e => new { e.ReservationId, e.StatusId }, "reservation_payment_proofs_reservation_status_idx");
        builder.HasIndex(e => e.FileId, "reservation_payment_proofs_file_id_idx");
        builder.HasIndex(e => e.ReviewedByUserId, "reservation_payment_proofs_reviewed_by_user_id_idx");

        builder.HasOne(e => e.Reservation)
            .WithMany(e => e.PaymentProofs)
            .HasForeignKey(e => e.ReservationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_payment_proofs_reservation_id_fkey");

        builder.HasOne(e => e.File)
            .WithMany()
            .HasForeignKey(e => e.FileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_payment_proofs_file_id_fkey");

        builder.HasOne(e => e.Status)
            .WithMany()
            .HasForeignKey(e => e.StatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_payment_proofs_status_id_fkey");

        builder.HasOne(e => e.ReviewedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("reservation_payment_proofs_reviewed_by_user_id_fkey");
    }
}
