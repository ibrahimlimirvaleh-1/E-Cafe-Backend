using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Localization;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public sealed class ContentTranslationRepository(
    ECafeDbContext context,
    IEnumerable<IContentTranslationStore> stores) : IContentTranslationRepository
{
    private readonly IReadOnlyDictionary<ContentEntityType, IContentTranslationStore> storesByType =
        stores.ToDictionary(store => store.Type);

    /// <summary>Qeydiyyatdan keçmiş tipli saxlayıcının mənbə proyeksiyasını tək sorğu ilə icra edir.</summary>
    public async Task<ContentTranslationSource?> GetSourceAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
        => await Store(type).SourceQuery(id).SingleOrDefaultAsync(cancellationToken);

    /// <summary>Eyni növün bütün tələb olunan tərcümələrini bir sorğuda gətirir; boş siyahı bazaya sorğu yaratmır.</summary>
    public async Task<IReadOnlyList<ContentTranslationRow>> GetRowsAsync(ContentEntityType type, IReadOnlyCollection<int> ids,
        string? language, CancellationToken cancellationToken)
    {
        var store = Store(type);
        return ids.Count == 0 ? [] : await store.RowsQuery(ids, language).ToListAsync(cancellationToken);
    }

    /// <summary>Entity yazılarını transaction daxilində əvvəlki ayrıca advisory-lock sahəsində ardıcıllaşdırır.</summary>
    public Task AcquireLockAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
    {
        _ = Store(type);
        return context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1})", [TranslationLockKeys.ContentNamespaceOffset + (int)type, id], cancellationToken);
    }

    /// <summary>Tərcümə yazısını seçilmiş tipli saxlayıcıya ötürür; reviziya nəzarəti saxlayıcıda icra olunur.</summary>
    public Task SaveAsync(ContentEntityType type, int id, int languageId, ContentTranslationValue value,
        long revision, CancellationToken cancellationToken)
        => Store(type).SaveAsync(id, languageId, value, revision, cancellationToken);

    /// <summary>Seçilmiş dil sətrinin reviziya ilə qorunan silinməsini uyğun saxlayıcıya yönləndirir.</summary>
    public Task DeleteAsync(ContentEntityType type, int id, int languageId, long expectedRevision, CancellationToken cancellationToken)
        => Store(type).DeleteAsync(id, languageId, expectedRevision, cancellationToken);

    /// <summary>Yalnız açıq qeydiyyatdan keçmiş məzmun növlərini qəbul edir; sərbəst entity və cədvəl adı həll etmir.</summary>
    private IContentTranslationStore Store(ContentEntityType type)
        => storesByType.TryGetValue(type, out var store) ? store : throw new ArgumentOutOfRangeException(nameof(type));
}
