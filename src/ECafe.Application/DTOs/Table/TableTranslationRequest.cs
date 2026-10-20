namespace ECafe.Application.DTOs.Table;

public sealed class TableTranslationRequest
{
    public string LanguageCode { get; set; } = null!;
    public string? Name { get; set; }
}
