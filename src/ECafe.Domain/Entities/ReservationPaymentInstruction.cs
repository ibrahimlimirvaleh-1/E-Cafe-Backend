using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class ReservationPaymentInstruction : AuditableSoftDeletableEntity<int>
{
    public int ReservationId { get; set; }

    // Kept only until the background worker encrypts records created before this change.
    public string? LegacyDisplayText { get; set; }

    public string? EncryptedDetails { get; set; }

    public string? MaskedDetails { get; set; }

    public decimal Amount { get; set; }

    public int SentByUserId { get; set; }

    public DateTime SentAt { get; set; }

    public virtual Reservation Reservation { get; set; } = null!;

    public virtual User SentByUser { get; set; } = null!;

    public virtual ICollection<ReservationPaymentProof> PaymentProofs { get; set; } = new List<ReservationPaymentProof>();
}
