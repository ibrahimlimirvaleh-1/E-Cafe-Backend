using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;
public partial class Reservation : BaseEntity<int>, IAuditable, ISoftDelete
{

    public int RestaurantId { get; set; }

    public int CustomerUserId { get; set; }

    public int TableId { get; set; }

    public int PeopleCount { get; set; }

    public int StatusId { get; set; }

    public DateTime ReservedFrom { get; set; }

    public DateTime ReservedTo { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? SeatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Note { get; set; }

    public virtual User CustomerUser { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual Restaurant Restaurant { get; set; } = null!;

    public virtual Status Status { get; set; } = null!;

    public virtual Table Table { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
