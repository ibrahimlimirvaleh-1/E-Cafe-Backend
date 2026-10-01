using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public sealed class ReservationArrivalAdjustment : AuditableSoftDeletableEntity<int>
{
    public int ReservationId { get; set; }
    public DateTime RequestedArrivalAt { get; set; }
    public DateTime OriginalNoShowDeadlineAt { get; set; }
    public DateTime MaximumNoShowDeadlineAt { get; set; }
    public DateTime ProposedNoShowDeadlineAt { get; set; }
    public DateTime? MustVacateAt { get; set; }
    public DateTime DecisionExpiresAt { get; set; }
    public Guid ConsentToken { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Reservation Reservation { get; set; } = null!;
}
