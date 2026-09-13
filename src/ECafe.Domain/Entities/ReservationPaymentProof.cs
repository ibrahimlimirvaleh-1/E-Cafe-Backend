using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class ReservationPaymentProof : AuditableSoftDeletableEntity<int>
{
    public int ReservationId { get; set; }

    public int FileId { get; set; }

    public decimal Amount { get; set; }

    public int StatusId { get; set; }

    public DateTime SubmittedAt { get; set; }

    public int? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectReason { get; set; }

    public virtual Reservation Reservation { get; set; } = null!;

    public virtual File File { get; set; } = null!;

    public virtual Status Status { get; set; } = null!;

    public virtual User? ReviewedByUser { get; set; }
}
