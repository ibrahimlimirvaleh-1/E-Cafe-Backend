using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class ReservationRefund : AuditableSoftDeletableEntity<int>
{
    public int ReservationId { get; set; }

    public int? SourcePaymentProofId { get; set; }

    public int StatusId { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "AZN";

    public int? InitiatedByUserId { get; set; }

    public DateTime RequestedAt { get; set; }

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public string EligibilityReason { get; set; } = null!;

    public string? CancellationReasonSnapshot { get; set; }

    public virtual Reservation Reservation { get; set; } = null!;

    public virtual ReservationPaymentProof? SourcePaymentProof { get; set; }

    public virtual Status Status { get; set; } = null!;

    public virtual User? InitiatedByUser { get; set; }

    public virtual User? ApprovedByUser { get; set; }

    public virtual ICollection<ReservationRefundTransfer> TransferAttempts { get; set; }
        = new List<ReservationRefundTransfer>();

    public virtual ICollection<ReservationRefundStatusHistory> StatusHistory { get; set; }
        = new List<ReservationRefundStatusHistory>();
}
