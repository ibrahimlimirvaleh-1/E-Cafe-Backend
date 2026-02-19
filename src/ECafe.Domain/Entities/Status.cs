using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class Status : AuditableSoftDeletableEntity<int>
{
    public string Name { get; set; } = null!;

    public int StatusTypeId { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> PaymentPaymentMethods { get; set; } = new List<Payment>();

    public virtual ICollection<Payment> PaymentPaymentStatuses { get; set; } = new List<Payment>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual StatusType StatusType { get; set; } = null!;
}
