using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class Order : AuditableSoftDeletableEntity<int>
{
    public int RestaurantId { get; set; }

    public int TableId { get; set; }

    public int? TableSessionId { get; set; }

    public int? ReservationId { get; set; }

    public int? CustomerUserId { get; set; }

    public int? WaiterUserId { get; set; }

    public int StatusId { get; set; }

    public DateTime OpenedAt { get; set; }

    public DateTime? ScheduledKitchenTime { get; set; }

    public DateTime? SentToKitchenAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? PreparingAt { get; set; }

    public DateTime? ReadyAt { get; set; }

    public DateTime? ServedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public int SourceId { get; set; }

    public string? Note { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual User? CustomerUser { get; set; }

    public virtual Reservation? Reservation { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;

    public virtual Status Status { get; set; } = null!;

    public virtual Table Table { get; set; } = null!;

    public virtual TableSession? TableSession { get; set; }

    public virtual User? WaiterUser { get; set; }

}
