using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Entities;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public sealed class ContentLanguageRepository(ECafeDbContext context) : IContentLanguageRepository
{
    /// <summary>Dil kataloqunu izləməsiz oxuyur; deaktiv dil məlumatını da yoxlamalar üçün saxlayır.</summary>
    public async Task<IReadOnlyList<ContentLanguage>> GetLanguagesAsync(CancellationToken cancellationToken)
        => await context.Set<Language>().AsNoTracking().OrderBy(language => language.Id)
            .Select(language => new ContentLanguage(language.Id, language.Code, language.IsActive, language.Name, language.IsDefault))
            .ToListAsync(cancellationToken);
}
