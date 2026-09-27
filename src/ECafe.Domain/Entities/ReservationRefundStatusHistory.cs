using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class ReservationRefundStatusHistory : AuditableSoftDeletableEntity<int>
{
    public int ReservationRefundId { get; set; }

    public int? FromStatusId { get; set; }

    public int ToStatusId { get; set; }

    public int? ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Reason { get; set; }

    public virtual ReservationRefund ReservationRefund { get; set; } = null!;

    public virtual Status? FromStatus { get; set; }

    public virtual Status ToStatus { get; set; } = null!;

    public virtual User? ChangedByUser { get; set; }
}
