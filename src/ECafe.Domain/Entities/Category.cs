using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;


public partial class Category : AuditableSoftDeletableEntity<int>
{
    public ICollection<CategoryTranslation> Translations { get; set; } = new List<CategoryTranslation>();

    public int RestaurantId { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();

    public virtual Restaurant Restaurant { get; set; } = null!;

}
