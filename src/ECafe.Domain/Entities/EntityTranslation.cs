namespace ECafe.Domain.Entities;

public abstract class EntityTranslation
{
    public int LanguageId { get; set; }
    public Language Language { get; set; } = null!;
    public long Revision { get; set; } = 1;
}
