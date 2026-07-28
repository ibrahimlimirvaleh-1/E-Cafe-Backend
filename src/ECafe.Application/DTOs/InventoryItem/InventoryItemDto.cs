namespace ECafe.Application.DTOs.InventoryItem
{
    public class InventoryItemDto
    {
        public int Id { get; set; }
        public int RestaurantId { get; set; }
        public string Name { get; set; } = null!;
        public int UnitId { get; set; }
        public string UnitName { get; set; } = null!;
        public string UnitCode { get; set; } = null!;
        public decimal QuantityOnHand { get; set; }
        public decimal LowStockThreshold { get; set; }
        public bool IsLowStock { get; set; }
        public bool IsActive { get; set; }
    }
}