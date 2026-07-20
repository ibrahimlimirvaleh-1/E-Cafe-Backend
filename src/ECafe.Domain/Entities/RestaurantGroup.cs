using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class RestaurantGroup : AuditableSoftDeletableEntity<int>
{
    public string Name { get; set; } = null!;

    public string? LegalName { get; set; }

    public int? OwnerUserId { get; set; }

    public bool IsActive { get; set; }

    public virtual User? OwnerUser { get; set; }

    public virtual ICollection<Restaurant> Restaurants { get; set; } = new List<Restaurant>();
}
