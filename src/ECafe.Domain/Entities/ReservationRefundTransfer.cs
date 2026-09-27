using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class ReservationRefundTransfer : AuditableSoftDeletableEntity<int>
{
    public int ReservationRefundId { get; set; }

    public decimal Amount { get; set; }

    public string? TransferReference { get; set; }

    public int? ProofFileId { get; set; }

    public int SubmittedByUserId { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? CustomerConfirmedAt { get; set; }

    public DateTime? DisputedAt { get; set; }

    public string? DisputeReason { get; set; }

    public virtual ReservationRefund ReservationRefund { get; set; } = null!;

    public virtual File? ProofFile { get; set; }

    public virtual User SubmittedByUser { get; set; } = null!;
}
