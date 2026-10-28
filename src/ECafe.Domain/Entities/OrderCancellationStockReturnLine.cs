namespace ECafe.Domain.Entities;

public sealed class OrderCancellationStockReturnLine
{
    public int Id { get; set; }
    public int CancellationRequestId { get; set; }
    public int InventoryItemId { get; set; }
    public int UnitId { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public decimal ReturnQuantity { get; set; }
    public OrderCancellationRequest CancellationRequest { get; set; } = null!;
}
