using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class Order : BaseEntity<int>, IAuditable, ISoftDelete
{
    public int RestaurantId { get; set; }

    public int TableId { get; set; }

    public int? ReservationId { get; set; }

    public int? WaiterUserId { get; set; }

    public int StatusId { get; set; }

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? Note { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual Reservation? Reservation { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;

    public virtual Status Status { get; set; } = null!;

    public virtual Table Table { get; set; } = null!;

    public virtual User? WaiterUser { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
