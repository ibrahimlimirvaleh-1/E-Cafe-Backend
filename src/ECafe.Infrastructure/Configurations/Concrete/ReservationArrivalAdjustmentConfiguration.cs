using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class ReservationArrivalAdjustmentConfiguration : DbEntityConfig<ReservationArrivalAdjustment>
{
    public override void Configure(EntityTypeBuilder<ReservationArrivalAdjustment> builder)
    {
        builder.ToTable("reservation_arrival_adjustments", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ReservationId).HasColumnName("reservation_id");
        builder.Property(x => x.RequestedArrivalAt).HasColumnName("requested_arrival_at");
        builder.Property(x => x.OriginalNoShowDeadlineAt).HasColumnName("original_no_show_deadline_at");
        builder.Property(x => x.MaximumNoShowDeadlineAt).HasColumnName("maximum_no_show_deadline_at");
        builder.Property(x => x.ProposedNoShowDeadlineAt).HasColumnName("proposed_no_show_deadline_at");
        builder.Property(x => x.MustVacateAt).HasColumnName("must_vacate_at");
        builder.Property(x => x.DecisionExpiresAt).HasColumnName("decision_expires_at");
        builder.Property(x => x.ConsentToken).HasColumnName("consent_token");
        builder.Property(x => x.AcceptedAt).HasColumnName("accepted_at");
        builder.HasIndex(x => x.ReservationId).IsUnique();
        builder.HasOne(x => x.Reservation).WithOne(x => x.ArrivalAdjustment)
            .HasForeignKey<ReservationArrivalAdjustment>(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
