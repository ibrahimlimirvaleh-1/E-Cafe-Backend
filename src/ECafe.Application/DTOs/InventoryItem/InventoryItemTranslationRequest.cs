namespace ECafe.Application.DTOs.InventoryItem;

public sealed class InventoryItemTranslationRequest
{
    public string LanguageCode { get; set; } = null!;
    public string Name { get; set; } = null!;
}
