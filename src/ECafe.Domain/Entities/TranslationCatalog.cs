namespace ECafe.Domain.Entities;

public sealed class TranslationCatalog
{
    public int Id { get; set; } = 1;
    public long Revision { get; set; }
    public int? PublishedReleaseId { get; set; }
    public string DraftJson { get; set; } = "{}";
}

public sealed class TranslationRelease
{
    public int Id { get; set; }
    public string MessagesJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public int? SourceReleaseId { get; set; }
}
