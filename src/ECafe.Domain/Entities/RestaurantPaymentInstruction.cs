using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class RestaurantPaymentInstruction : AuditableSoftDeletableEntity<int>
{
    public int RestaurantId { get; set; }

    public string Title { get; set; } = null!;

    public string PaymentMethod { get; set; } = null!;

    public string DisplayText { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;
}
