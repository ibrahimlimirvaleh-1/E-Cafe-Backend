using ECafe.Domain.Entities;

namespace ECafe.Application.Repositories.Localization;

public sealed record TranslationReleaseMetadata(int Id, DateTime CreatedAt, int CreatedByUserId, int? SourceReleaseId);

public interface ITranslationRepository
{
    Task<TranslationCatalog?> GetCatalogAsync(bool tracked, CancellationToken cancellationToken);
    Task<TranslationRelease?> GetReleaseAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TranslationReleaseMetadata>> GetReleasesAsync(CancellationToken cancellationToken);
    Task AddReleaseAsync(TranslationRelease release, CancellationToken cancellationToken);
    Task AcquireLockAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
