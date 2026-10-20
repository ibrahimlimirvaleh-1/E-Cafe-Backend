namespace ECafe.Domain.Entities;

public sealed class TranslationRelease
{
    public int Id { get; set; }
    public ICollection<TranslationReleaseEntry> Entries { get; set; } = new List<TranslationReleaseEntry>();
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public int? SourceReleaseId { get; set; }
}
