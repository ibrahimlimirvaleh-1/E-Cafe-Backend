namespace ECafe.Domain.Entities;

public sealed class Keyword
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public ICollection<KeywordTranslation> Translations { get; set; } = new List<KeywordTranslation>();
}
