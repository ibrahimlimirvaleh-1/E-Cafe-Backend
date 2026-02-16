using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class UserRestaurant :  IAuditable, ISoftDelete
{
    public int UserId { get; set; }

    public int RestaurantId { get; set; }

    public bool IsActive { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;

    public virtual User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
