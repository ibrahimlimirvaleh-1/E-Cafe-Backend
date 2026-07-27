namespace ECafe.Application.DTOs.InventoryItem
{
    public class CreateInventoryItemRequest
    {
        public int RestaurantId { get; set; }
        public string Name { get; set; } = null!;
        public int UnitId { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal LowStockThreshold { get; set; }
    }
}
