using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class UserRestaurant : AuditableSoftDeletableEntity<int>
{
    public int UserId { get; set; }

    public int RestaurantId { get; set; }

    public bool IsActive { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
