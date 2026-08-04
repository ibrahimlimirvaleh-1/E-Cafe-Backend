using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class TableSession : AuditableSoftDeletableEntity<int>
{
    public int RestaurantId { get; set; }

    public int TableId { get; set; }

    public int? CustomerUserId { get; set; }

    public int? WaiterUserId { get; set; }

    public int? ReservationId { get; set; }

    public int StatusId { get; set; }

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? Note { get; set; }

    public virtual User? CustomerUser { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual Reservation? Reservation { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;

    public virtual Status Status { get; set; } = null!;

    public virtual Table Table { get; set; } = null!;

    public virtual User? WaiterUser { get; set; }
}
