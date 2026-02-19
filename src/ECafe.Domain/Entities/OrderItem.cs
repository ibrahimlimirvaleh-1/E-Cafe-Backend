using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;
public partial class OrderItem : AuditableSoftDeletableEntity<int>
{
    public int OrderId { get; set; }

    public int ItemId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public string? Note { get; set; }

    public virtual Item Item { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

}
