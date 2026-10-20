namespace ECafe.Application.DTOs.InventoryItem
{
    public class CreateInventoryItemRequest
    {
        public List<InventoryItemTranslationRequest>? Translations { get; set; }
        public string Name { get; set; } = null!;
        public int UnitId { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal LowStockThreshold { get; set; }
    }
}
