using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class Restaurant : BaseEntity<int>, IAuditable, ISoftDelete
{
    public string Name { get; set; } = null!;

    public string Location { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public decimal RatingAverage { get; set; }

    public int RatingCount { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();

    public virtual ICollection<UserRestaurant> UserRestaurants { get; set; } = new List<UserRestaurant>();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
