namespace ECafe.Application.DTOs.InventoryMovement
{
    public class InventoryMovementHistoryResponse
    {
        public int Id { get; set; }
        public int RestaurantId { get; set; }
        public int InventoryItemId { get; set; }
        public decimal QuantityChange { get; set; }
        public int UnitId { get; set; }
        public string UnitName { get; set; } = null!;
        public int MovementTypeId { get; set; }
        public string MovementType { get; set; } = null!;
        public string RelatedOrderId { get; set; } = null!;
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; }

    }
}
