using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class Reservation : AuditableSoftDeletableEntity<int>
{

    public int RestaurantId { get; set; }

    public int CustomerUserId { get; set; }

    public int TableId { get; set; }

    public int? WaiterUserId { get; set; }

    public int PeopleCount { get; set; }

    public int StatusId { get; set; }

    public decimal DepositAmount { get; set; }

    public int CancellationWindowMinutes { get; set; }

    public DateTime? CancellationDeadline { get; set; }

    public DateTime ReservedFrom { get; set; }

    public DateTime ReservedTo { get; set; }

    public DateTime? CancelledAt { get; set; }

    public bool? RefundEligible { get; set; }

    public DateTime? NoShowAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? SeatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int? CheckedInByUserId { get; set; }

    public int? CancelledByUserId { get; set; }

    public int? NoShowByUserId { get; set; }

    public int? CompletedByUserId { get; set; }

    public string? Note { get; set; }

    public virtual User? CancelledByUser { get; set; }

    public virtual User? CheckedInByUser { get; set; }

    public virtual User? CompletedByUser { get; set; }

    public virtual User CustomerUser { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<TableSession> TableSessions { get; set; } = new List<TableSession>();

    public virtual User? NoShowByUser { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;

    public virtual Status Status { get; set; } = null!;

    public virtual Table Table { get; set; } = null!;

    public virtual User? WaiterUser { get; set; }
}
