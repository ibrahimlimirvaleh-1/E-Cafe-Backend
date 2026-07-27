using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class InventoryItem : AuditableSoftDeletableEntity<int>
    {
        public int RestaurantId { get; set; }
        public string Name { get; set; } = null!;
        public int UnitId { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal LowStockThreshold { get; set; }
        public DateTime? LastLowStockNotifiedAt { get; set; }
        public bool IsActive { get; set; }

        public Restaurant Restaurant { get; set; } = null!;
        public Unit Unit { get; set; } = null!;
        public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
        public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
    }
}
