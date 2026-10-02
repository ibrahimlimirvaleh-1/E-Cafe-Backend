using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class RestaurantScheduleChangeConfiguration : DbEntityConfig<RestaurantScheduleChange>
{
    public override void Configure(EntityTypeBuilder<RestaurantScheduleChange> b)
    {
        b.ToTable("restaurant_schedule_changes", "ops");
        b.HasKey(c => c.Id);
        b.Property(c => c.ProposedHoursJson).HasColumnType("jsonb").IsRequired();
        b.Property(c => c.Reason).HasMaxLength(1000).IsRequired();
        b.HasIndex(c => c.RestaurantId).IsUnique().HasFilter("\"State\" = 0 AND \"IsDeleted\" = false");
        b.HasOne(c => c.Restaurant).WithMany().HasForeignKey(c => c.RestaurantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RestaurantScheduleConsentConfiguration : DbEntityConfig<RestaurantScheduleConsent>
{
    public override void Configure(EntityTypeBuilder<RestaurantScheduleConsent> b)
    {
        b.ToTable("restaurant_schedule_consents", "ops", table => table.HasCheckConstraint(
            "ck_schedule_consent_target", "(\"ReservationId\" IS NULL) <> (\"TableSessionId\" IS NULL)"));
        b.HasKey(c => c.Id);
        b.Property(c => c.ResponseNote).HasMaxLength(1000);
        b.HasOne(c => c.ScheduleChange).WithMany(c => c.Consents).HasForeignKey(c => c.ScheduleChangeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(c => c.Reservation).WithMany().HasForeignKey(c => c.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(c => c.TableSession).WithMany().HasForeignKey(c => c.TableSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => new { c.ScheduleChangeId, c.ReservationId }).IsUnique();
        b.HasIndex(c => new { c.ScheduleChangeId, c.TableSessionId }).IsUnique();
    }
}
