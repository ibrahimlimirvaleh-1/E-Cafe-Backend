namespace ECafe.Domain.Entities;

public sealed class KeywordTranslation
{
    public int KeywordId { get; set; }
    public int LanguageId { get; set; }
    public string Value { get; set; } = string.Empty;
    public Keyword Keyword { get; set; } = null!;
    public Language Language { get; set; } = null!;
}
