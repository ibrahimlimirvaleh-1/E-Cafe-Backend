using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class RestaurantDepositRule : AuditableSoftDeletableEntity<int>
{
    public int RestaurantId { get; set; }
    public DateOnly ReservationDate { get; set; }
    public decimal Amount { get; set; }
    public virtual Restaurant Restaurant { get; set; } = null!;
}
