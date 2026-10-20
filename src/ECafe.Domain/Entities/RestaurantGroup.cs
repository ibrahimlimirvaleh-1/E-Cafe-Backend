using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class RestaurantGroup : AuditableSoftDeletableEntity<int>
{
    public ICollection<RestaurantGroupTranslation> Translations { get; set; } = new List<RestaurantGroupTranslation>();

    public string Name { get; set; } = null!;

    public string? LegalName { get; set; }

    public bool IsActive { get; set; }

    public string? Email { get; set; }

    public virtual ICollection<Restaurant> Restaurants { get; set; } = new List<Restaurant>();
}
