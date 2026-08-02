using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class Recipe : AuditableSoftDeletableEntity<int>
    {
        public int RestaurantId { get; set; }
        public int ItemId { get; set; }
        public int InventoryItemId { get; set; }
        public decimal Quantity { get; set; }
        public int UnitId { get; set; }
        public bool IsActive { get; set; }

        public Item Item { get; set; } = null!;
        public InventoryItem InventoryItem { get; set; } = null!;
        public Unit Unit { get; set; } = null!;
    }
}
