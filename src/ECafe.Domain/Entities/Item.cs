using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;



public partial class Item : BaseEntity<int>, IAuditable, ISoftDelete
{
    public int RestaurantId { get; set; }

    public int CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal BasePrice { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; }

    public string? UnavailableReason { get; set; }

    public int SalesCount { get; set; }

    public bool IsActive { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Restaurant Restaurant { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
