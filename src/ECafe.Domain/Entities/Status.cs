using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class Status : BaseEntity<int>, IAuditable, ISoftDelete
{
    public string Name { get; set; } = null!;

    public int StatusTypeId { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> PaymentPaymentMethods { get; set; } = new List<Payment>();

    public virtual ICollection<Payment> PaymentPaymentStatuses { get; set; } = new List<Payment>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual StatusType StatusType { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
