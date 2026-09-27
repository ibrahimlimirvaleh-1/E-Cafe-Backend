using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class ReservationRefundPayoutDetail : AuditableSoftDeletableEntity<int>
{
    public int ReservationRefundId { get; set; }

    public string EncryptedDetails { get; set; } = null!;

    public string MaskedDetails { get; set; } = null!;

    public int SubmittedByUserId { get; set; }

    public DateTime SubmittedAt { get; set; }

    public virtual ReservationRefund ReservationRefund { get; set; } = null!;

    public virtual User SubmittedByUser { get; set; } = null!;
}
