using System.Globalization;
using ECafe.Application.DTOs.Localization;
using ECafe.Domain.Entities;
using ECafe.Domain.Policies;
using ECafe.Application.Repositories.Localization;

namespace ECafe.Application.Mappings;

public static class TranslationMapper
{
    /// <summary>İşçi qaralamanı yalnız administrator üçün dil sütunları ilə göstərir; daxili JSON quruluşunu UI-yə bağlamır.</summary>
    public static TranslationDraftResponse Draft(TranslationCatalog catalog, IReadOnlyDictionary<string, TranslationText> entries)
        => new(catalog.Revision, catalog.PublishedReleaseId?.ToString(CultureInfo.InvariantCulture), entries
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new TranslationEntryResponse(pair.Key, pair.Value.Az, pair.Value.En, pair.Value.Ru)).ToArray());
    /// <summary>Yayım tarixçəsini dəyişdirilməz mətnlərin özünü daşımadan metadata şəklində qaytarır.</summary>
    public static TranslationReleaseResponse Release(TranslationRelease release, long revision)
        => Release(new TranslationReleaseMetadata(release.Id, release.CreatedAt, release.CreatedByUserId, release.SourceReleaseId), revision);
    /// <summary>Yüngül tarixçə proyeksiyasını aktiv qaralama reviziyası ilə UI müqaviləsinə çevirir.</summary>
    public static TranslationReleaseResponse Release(TranslationReleaseMetadata release, long revision)
        => new(release.Id, revision, new DateTimeOffset(DateTime.SpecifyKind(release.CreatedAt, DateTimeKind.Utc)), release.CreatedByUserId, release.SourceReleaseId);
}
