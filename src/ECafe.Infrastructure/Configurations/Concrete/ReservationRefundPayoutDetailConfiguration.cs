using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class ReservationRefundPayoutDetailConfiguration
    : DbEntityConfig<ReservationRefundPayoutDetail>
{
    public override void Configure(EntityTypeBuilder<ReservationRefundPayoutDetail> builder)
    {
        builder.HasKey(e => e.Id).HasName("reservation_refund_payout_details_pkey");
        builder.ToTable("reservation_refund_payout_details", "billing");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ReservationRefundId).HasColumnName("reservation_refund_id");
        builder.Property(e => e.EncryptedDetails).HasMaxLength(4000).HasColumnName("encrypted_details");
        builder.Property(e => e.MaskedDetails).HasMaxLength(100).HasColumnName("masked_details");
        builder.Property(e => e.SubmittedByUserId).HasColumnName("submitted_by_user_id");
        builder.Property(e => e.SubmittedAt).HasColumnName("submitted_at");

        builder.HasIndex(e => e.ReservationRefundId, "reservation_refund_payout_details_refund_id_key")
            .IsUnique();

        builder.HasOne(e => e.ReservationRefund)
            .WithOne(e => e.PayoutDetails)
            .HasForeignKey<ReservationRefundPayoutDetail>(e => e.ReservationRefundId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_refund_payout_details_refund_id_fkey");

        builder.HasOne(e => e.SubmittedByUser)
            .WithMany()
            .HasForeignKey(e => e.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_refund_payout_details_submitted_by_user_id_fkey");
    }
}
