using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class InventoryMovement : AuditableSoftDeletableEntity<int>
    {
        public int RestaurantId { get; set; }
        public int InventoryItemId { get; set; }
        public decimal QuantityChange { get; set; }
        public int UnitId { get; set; }
        public string MovementType { get; set; } = null!;
        public string Reason { get; set; } = null!;
        public int? RelatedOrderId { get; set; }

        public InventoryItem InventoryItem { get; set; } = null!;
        public Unit Unit { get; set; } = null!;
    }
}
