namespace ECafe.Application.DTOs.InventoryItem
{
    public class UpdateInventoryItemRequest
    {
        public string Name { get; set; } = null!;
        public int UnitId { get; set; }
        public decimal LowStockThreshold { get; set; }
        public bool IsActive { get; set; }
    }
}
