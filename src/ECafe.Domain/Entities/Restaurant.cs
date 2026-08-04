using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class Restaurant : AuditableSoftDeletableEntity<int>
{
    public string Name { get; set; } = null!;

    public string Location { get; set; } = null!;

    public string Phone { get; set; } = null!;
    public string Email { get; set; } = null!;

    public int? RestaurantGroupId { get; set; }

    public string? BranchName { get; set; }

    public decimal? RatingAverage { get; set; }

    public int? RatingCount { get; set; }

    public decimal DepositAmount { get; set; }

    public int CancellationWindowMinutes { get; set; }

    public decimal ServiceFeePercent { get; set; }

    public int StaffSettlementPeriod { get; set; }

    public int? DefaultWaiterTableLimit { get; set; }

    public bool IsActive { get; set; }

    public virtual List<File>? Files { get; set; }

    public virtual RestaurantGroup? RestaurantGroup { get; set; }

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual ICollection<TableSession> TableSessions { get; set; } = new List<TableSession>();

    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();

    public virtual ICollection<UserRestaurant> UserRestaurants { get; set; } = new List<UserRestaurant>();

    public virtual ICollection<RestaurantContract> Contracts { get; set; } = new List<RestaurantContract>();
}
