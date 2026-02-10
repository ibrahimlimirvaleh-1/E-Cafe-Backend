


namespace ECafe.Domain.Entities;


public partial class Order
{
    public int Id { get; set; }

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
}
