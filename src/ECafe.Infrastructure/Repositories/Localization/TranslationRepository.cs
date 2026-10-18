using ECafe.Application.Repositories.Localization;
using ECafe.Domain.Entities;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Localization;

public sealed class TranslationRepository(ECafeDbContext context) : ITranslationRepository
{
    public Task<TranslationCatalog?> GetCatalogAsync(bool tracked, CancellationToken cancellationToken)
        => (tracked ? context.TranslationCatalogs.AsQueryable() : context.TranslationCatalogs.AsNoTracking())
            .SingleOrDefaultAsync(catalog => catalog.Id == 1, cancellationToken);
    public Task<TranslationRelease?> GetReleaseAsync(int id, CancellationToken cancellationToken)
        => context.TranslationReleases.AsNoTracking().SingleOrDefaultAsync(release => release.Id == id, cancellationToken);
    /// <summary>Son 50 yayımın yalnız metadata-sını proyeksiya edir; böyük lüğət snapshot-larını tarixçə ekranı üçün DB-dən yükləmir.</summary>
    public async Task<IReadOnlyList<TranslationReleaseMetadata>> GetReleasesAsync(CancellationToken cancellationToken)
        => await context.TranslationReleases.AsNoTracking().OrderByDescending(release => release.Id).Take(50)
            .Select(release => new TranslationReleaseMetadata(release.Id, release.CreatedAt, release.CreatedByUserId, release.SourceReleaseId))
            .ToListAsync(cancellationToken);
    public async Task AddReleaseAsync(TranslationRelease release, CancellationToken cancellationToken)
        => await context.TranslationReleases.AddAsync(release, cancellationToken);
    /// <summary>Aztezlikli admin yazılarını ayrıca PostgreSQL transaction advisory kilidi ilə ardıcıl edir; masa və istifadəçi kilidlərinə toxunmur.</summary>
    public Task AcquireLockAsync(CancellationToken cancellationToken)
        => context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(-19001, 1)", cancellationToken);
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await context.SaveChangesAsync(cancellationToken);
}
