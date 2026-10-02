using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public enum ScheduleChangeState { Pending, Applied, Withdrawn }
public enum ScheduleConsentState { Pending, Accepted, Rejected }

public sealed class RestaurantScheduleChange : AuditableSoftDeletableEntity<int>
{
    public int RestaurantId { get; set; }
    public string ProposedHoursJson { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public Guid Token { get; set; }
    public ScheduleChangeState State { get; set; }
    public int RequestedByUserId { get; set; }
    public DateTime? AppliedAt { get; set; }
    public Restaurant Restaurant { get; set; } = null!;
    public ICollection<RestaurantScheduleConsent> Consents { get; set; } = new List<RestaurantScheduleConsent>();
}

public sealed class RestaurantScheduleConsent : AuditableSoftDeletableEntity<int>
{
    public int ScheduleChangeId { get; set; }
    public int? ReservationId { get; set; }
    public int? TableSessionId { get; set; }
    public DateTime? ProposedVacateAt { get; set; }
    public ScheduleConsentState State { get; set; }
    public DateTime? RespondedAt { get; set; }
    public int? RespondedByUserId { get; set; }
    public string? ResponseNote { get; set; }
    public RestaurantScheduleChange ScheduleChange { get; set; } = null!;
    public Reservation? Reservation { get; set; }
    public TableSession? TableSession { get; set; }
}
