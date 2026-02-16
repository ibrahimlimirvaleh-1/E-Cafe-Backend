using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class Table : BaseEntity<int>, IAuditable, ISoftDelete
{
    public int RestaurantId { get; set; }

    public int TableNo { get; set; }

    public string? Name { get; set; }

    public int Capacity { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual Restaurant Restaurant { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
