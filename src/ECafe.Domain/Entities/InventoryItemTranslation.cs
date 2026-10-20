namespace ECafe.Domain.Entities;

public sealed class InventoryItemTranslation : EntityTranslation
{
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public string? Name { get; set; }
}
