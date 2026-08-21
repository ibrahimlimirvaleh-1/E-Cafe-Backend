namespace ECafe.Application.DTOs.InventoryMovement
{
    public class CreateInventoryMovementRequest
    {
        public decimal Quantity { get; set; }
        public int UnitId { get; set; }
        public int MovementTypeId { get; set; }
        public string? Reason { get; set; }
    }
}
