namespace ECafe.Application.DTOs.Category;

public sealed class CategoryTranslationRequest
{
    public string LanguageCode { get; set; } = null!;
    public string Name { get; set; } = null!;
}
