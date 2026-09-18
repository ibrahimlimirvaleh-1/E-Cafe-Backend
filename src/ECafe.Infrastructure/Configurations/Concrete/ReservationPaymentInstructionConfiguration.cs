using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public class ReservationPaymentInstructionConfiguration : DbEntityConfig<ReservationPaymentInstruction>
{
    public override void Configure(EntityTypeBuilder<ReservationPaymentInstruction> builder)
    {
        builder.HasKey(e => e.Id).HasName("reservation_payment_instructions_pkey");

        builder.ToTable("reservation_payment_instructions", "billing");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ReservationId).HasColumnName("reservation_id");
        builder.Property(e => e.DisplayText)
            .HasMaxLength(1000)
            .HasColumnName("display_text");
        builder.Property(e => e.Amount)
            .HasPrecision(18, 2)
            .HasColumnName("amount");
        builder.Property(e => e.SentByUserId).HasColumnName("sent_by_user_id");
        builder.Property(e => e.SentAt)
            .HasColumnName("sent_at")
            .HasDefaultValueSql("now()");

        builder.HasIndex(e => new { e.ReservationId, e.SentAt }, "reservation_payment_instructions_reservation_sent_at_idx");

        builder.HasOne(e => e.Reservation)
            .WithMany(e => e.PaymentInstructions)
            .HasForeignKey(e => e.ReservationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_payment_instructions_reservation_id_fkey");

        builder.HasOne(e => e.SentByUser)
            .WithMany()
            .HasForeignKey(e => e.SentByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("reservation_payment_instructions_sent_by_user_id_fkey");
    }
}
