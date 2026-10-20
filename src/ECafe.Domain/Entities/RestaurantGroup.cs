using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class RestaurantGroup : AuditableSoftDeletableEntity<int>
{
    public string Name { get; set; } = null!;

    public string? LegalName { get; set; }

    public bool IsActive { get; set; }

    public string? Email { get; set; }

    public virtual ICollection<Restaurant> Restaurants { get; set; } = new List<Restaurant>();
}
