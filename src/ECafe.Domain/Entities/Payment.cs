using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class Payment : AuditableSoftDeletableEntity<int>
{
    public int RestaurantId { get; set; }

    public int? ReservationId { get; set; }

    public int? OrderId { get; set; }

    public decimal Amount { get; set; }

    public int PaymentMethodId { get; set; }

    public int PaymentStatusId { get; set; }

    public string? ProviderRef { get; set; }

    public DateTime? PaidAt { get; set; }

    public int? CreatedByUserId { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Status  PaymentMethod { get; set; } = null!;

    public virtual Status PaymentStatus { get; set; } = null!;

    public virtual Reservation? Reservation { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;

}
