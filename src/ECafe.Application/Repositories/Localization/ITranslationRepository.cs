using ECafe.Domain.Entities;
using ECafe.Domain.Policies;

namespace ECafe.Application.Repositories.Localization;

public sealed record TranslationReleaseMetadata(int Id, DateTime CreatedAt, int CreatedByUserId, int? SourceReleaseId);

public interface ITranslationRepository
{
    Task<TranslationCatalog?> GetCatalogAsync(bool tracked, CancellationToken cancellationToken);
    Task<TranslationRelease?> GetReleaseAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TranslationReleaseMetadata>> GetReleasesAsync(CancellationToken cancellationToken);
    Task<Dictionary<string, TranslationText>> GetDraftEntriesAsync(CancellationToken cancellationToken);
    Task<Dictionary<string, TranslationText>> GetReleaseEntriesAsync(int releaseId, CancellationToken cancellationToken);
    Task SaveDraftEntriesAsync(IReadOnlyDictionary<string, TranslationText> entries, CancellationToken cancellationToken);
    Task AddReleaseAsync(TranslationRelease release, IReadOnlyDictionary<string, TranslationText> entries, CancellationToken cancellationToken);
    Task AcquireLockAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
