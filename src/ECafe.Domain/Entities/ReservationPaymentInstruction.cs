using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class ReservationPaymentInstruction : AuditableSoftDeletableEntity<int>
{
    public int ReservationId { get; set; }

    public string DisplayText { get; set; } = null!;

    public decimal Amount { get; set; }

    public int SentByUserId { get; set; }

    public DateTime SentAt { get; set; }

    public virtual Reservation Reservation { get; set; } = null!;

    public virtual User SentByUser { get; set; } = null!;
}
