namespace ECafe.Domain.Entities;

public sealed class TranslationReleaseEntry
{
    public int TranslationReleaseId { get; set; }
    public int KeywordId { get; set; }
    public int LanguageId { get; set; }
    public string Value { get; set; } = string.Empty;
    public TranslationRelease TranslationRelease { get; set; } = null!;
    public Keyword Keyword { get; set; } = null!;
    public Language Language { get; set; } = null!;
}
