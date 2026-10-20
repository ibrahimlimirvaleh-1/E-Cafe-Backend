using ECafe.Domain.Constants;

namespace ECafe.Domain.Entities;

public sealed class TranslationCatalog
{
    public int Id { get; set; } = LocalizationDefaults.CatalogId;
    public long Revision { get; set; }
    public int? PublishedReleaseId { get; set; }
}
