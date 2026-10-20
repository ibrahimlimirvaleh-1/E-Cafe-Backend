using ECafe.Application.Repositories.Localization;
using ECafe.Domain.Entities;
using ECafe.Domain.Policies;
using ECafe.Domain.Constants;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Localization;

public sealed class TranslationRepository(ECafeDbContext context) : ITranslationRepository
{
    public Task<TranslationCatalog?> GetCatalogAsync(bool tracked, CancellationToken cancellationToken)
        => (tracked ? context.TranslationCatalogs.AsQueryable() : context.TranslationCatalogs.AsNoTracking())
            .SingleOrDefaultAsync(catalog => catalog.Id == LocalizationDefaults.CatalogId, cancellationToken);
    public Task<TranslationRelease?> GetReleaseAsync(int id, CancellationToken cancellationToken)
        => context.TranslationReleases.AsNoTracking().SingleOrDefaultAsync(release => release.Id == id, cancellationToken);
    /// <summary>Son 50 yayımın yalnız metadata-sını proyeksiya edir; böyük lüğət snapshot-larını tarixçə ekranı üçün DB-dən yükləmir.</summary>
    public async Task<IReadOnlyList<TranslationReleaseMetadata>> GetReleasesAsync(CancellationToken cancellationToken)
        => await context.TranslationReleases.AsNoTracking().OrderByDescending(release => release.Id).Take(50)
            .Select(release => new TranslationReleaseMetadata(release.Id, release.CreatedAt, release.CreatedByUserId, release.SourceReleaseId))
            .ToListAsync(cancellationToken);
    /// <summary>Qaralama açarlarını və üç dil mətnini tək proyeksiya ilə oxuyur; ayrıca sətir sorğuları yaratmır.</summary>
    public async Task<Dictionary<string, TranslationText>> GetDraftEntriesAsync(CancellationToken cancellationToken)
        => BuildDictionary(await context.Set<KeywordTranslation>().AsNoTracking()
            .Select(row => new TranslationRow(row.Keyword.Key, row.Language.Code, row.Value)).ToListAsync(cancellationToken));

    /// <summary>Seçilmiş dəyişməz yayımın sətirlərini oxuyur; cari qaralama mətnləri tarixçəyə təsir etmir.</summary>
    public async Task<Dictionary<string, TranslationText>> GetReleaseEntriesAsync(int releaseId, CancellationToken cancellationToken)
        => BuildDictionary(await context.Set<TranslationReleaseEntry>().AsNoTracking()
            .Where(row => row.TranslationReleaseId == releaseId)
            .Select(row => new TranslationRow(row.Keyword.Key, row.Language.Code, row.Value)).ToListAsync(cancellationToken));

    /// <summary>Admin transaction kilidi daxilində açarları yaradır və hər dilin qaralama sətrini əlavə edir və ya yeniləyir.</summary>
    public async Task SaveDraftEntriesAsync(IReadOnlyDictionary<string, TranslationText> entries, CancellationToken cancellationToken)
    {
        var languages = await GetLanguageIdsAsync(cancellationToken);
        var keywords = await GetKeywordsAsync(cancellationToken);
        foreach (var pair in entries)
        {
            var keyword = GetOrAddKeyword(keywords, pair.Key);
            foreach (var language in languages)
            {
                var translation = keyword.Translations.SingleOrDefault(row => row.LanguageId == language.Value);
                if (translation == null)
                {
                    translation = new KeywordTranslation { Keyword = keyword, LanguageId = language.Value };
                    keyword.Translations.Add(translation);
                }
                translation.Value = GetText(pair.Value, language.Key);
            }
        }
    }

    /// <summary>Üç dilin mətnlərini yeni yayımın ayrıca sətirləri kimi əlavə edir; əvvəlki yayım sətirlərini dəyişmir.</summary>
    public async Task AddReleaseAsync(TranslationRelease release, IReadOnlyDictionary<string, TranslationText> entries, CancellationToken cancellationToken)
    {
        var languages = await GetLanguageIdsAsync(cancellationToken);
        var keywords = await GetKeywordsAsync(cancellationToken);
        foreach (var pair in entries)
        {
            var keyword = GetOrAddKeyword(keywords, pair.Key);
            foreach (var language in languages)
                release.Entries.Add(new TranslationReleaseEntry
                { Keyword = keyword, LanguageId = language.Value, Value = GetText(pair.Value, language.Key) });
        }
        await context.TranslationReleases.AddAsync(release, cancellationToken);
    }

    /// <summary>Dil identifikatorlarını cədvəldən həll edir; üç dəstəklənən dil olmadan natamam kataloq yazılmasına icazə vermir.</summary>
    private async Task<Dictionary<string, int>> GetLanguageIdsAsync(CancellationToken cancellationToken)
    {
        var codes = TranslationPolicy.Languages.ToArray();
        var languages = await context.Set<Language>().AsNoTracking().Where(language => codes.Contains(language.Code))
            .ToDictionaryAsync(language => language.Code, language => language.Id, cancellationToken);
        if (languages.Count != TranslationPolicy.Languages.Count)
            throw new InvalidOperationException("The translation catalog requires all supported language rows.");
        return languages;
    }

    /// <summary>Qaralama sətirlərini toplu yükləyir və eyni transaction-dakı hələ saxlanılmamış açarları da nəzərə alır.</summary>
    private async Task<Dictionary<string, Keyword>> GetKeywordsAsync(CancellationToken cancellationToken)
    {
        var persisted = await context.Set<Keyword>().Include(keyword => keyword.Translations).ToListAsync(cancellationToken);
        return persisted.Concat(context.Set<Keyword>().Local).Distinct()
            .ToDictionary(keyword => keyword.Key, StringComparer.Ordinal);
    }

    /// <summary>Sabit açarı mövcud obyektə bağlayır və yalnız çatışmayan açar üçün yeni obyekt yaradır.</summary>
    private Keyword GetOrAddKeyword(IDictionary<string, Keyword> keywords, string key)
    {
        if (keywords.TryGetValue(key, out var keyword)) return keyword;
        keyword = new Keyword { Key = key };
        context.Set<Keyword>().Add(keyword);
        keywords.Add(key, keyword);
        return keyword;
    }

    /// <summary>Yalnız üç dili tamamlanmış açarları lüğətə çevirir; köhnə natamam qeydlər build ehtiyat mətninə qayıdır.</summary>
    private static Dictionary<string, TranslationText> BuildDictionary(IEnumerable<TranslationRow> rows)
        => rows.GroupBy(row => row.Key, StringComparer.Ordinal)
            .Select(group => new { group.Key, Texts = group.ToDictionary(row => row.Language, row => row.Value, StringComparer.Ordinal) })
            .Where(group => TranslationPolicy.Languages.All(language => group.Texts.TryGetValue(language, out var value) && !string.IsNullOrWhiteSpace(value)))
            .ToDictionary(group => group.Key, group => new TranslationText(
                group.Texts[TranslationPolicy.Code(LanguageCode.Az)], group.Texts[TranslationPolicy.Code(LanguageCode.En)],
                group.Texts[TranslationPolicy.Code(LanguageCode.Ru)]), StringComparer.Ordinal);

    private sealed record TranslationRow(string Key, string Language, string Value);

    /// <summary>Təsdiqlənmiş dil kodu üçün mətn sahəsini seçir; yeni dil səssizcə başqa dilin mətni ilə doldurulmur.</summary>
    private static string GetText(TranslationText text, string language)
        => TranslationPolicy.TextFor(text, TranslationPolicy.ParseLanguage(language));
    /// <summary>Aztezlikli admin yazılarını ayrıca PostgreSQL transaction advisory kilidi ilə ardıcıl edir; masa və istifadəçi kilidlərinə toxunmur.</summary>
    public Task AcquireLockAsync(CancellationToken cancellationToken)
        => context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1})",
            [TranslationLockKeys.CatalogNamespace, LocalizationDefaults.CatalogId], cancellationToken);
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await context.SaveChangesAsync(cancellationToken);
}
