namespace ECafe.Application.DTOs.InventoryMovement
{
    public class InventoryMovementResponse
    {
        public int Id { get; set; }
        public int RestaurantId { get; set; }
        public int InventoryItemId { get; set; }
        public decimal QuantityChange { get; set; }
        public int UnitId { get; set; }
        public string UnitName { get; set; } = null!;
        public int MovementTypeId { get; set; }
        public string MovementType { get; set; } = null!;
        public string MovementTypeCode { get; set; } = null!;
        public string Reason { get; set; } = null!;

        public decimal QuantityAfterMovement { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
