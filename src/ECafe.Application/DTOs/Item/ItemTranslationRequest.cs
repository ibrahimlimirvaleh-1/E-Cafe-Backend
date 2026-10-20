namespace ECafe.Application.DTOs.Item;

public sealed class ItemTranslationRequest
{
    public string LanguageCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? UnavailableReason { get; set; }
}
