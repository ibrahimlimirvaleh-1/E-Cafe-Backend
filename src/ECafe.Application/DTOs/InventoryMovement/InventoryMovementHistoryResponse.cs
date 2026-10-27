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
        public string UnitCode { get; set; } = null!;
        public int MovementTypeId { get; set; }
        public string MovementType { get; set; } = null!;
        public string MovementTypeCode { get; set; } = null!;
        public string RelatedOrderId { get; set; } = null!;
        public int? RelatedOrderItemId { get; set; }
        public string? OrderItemName { get; set; }
        public int? OrderItemQuantity { get; set; }
        public int? TableNo { get; set; }
        public string TimeZone { get; set; } = null!;
        public int? ReversedMovementId { get; set; }
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; }

    }
}
