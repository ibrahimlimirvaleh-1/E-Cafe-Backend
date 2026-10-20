using ECafe.Domain.Enums;

namespace ECafe.Application.Repositories.ContentLocalization;

public interface IContentTranslationQuery
{
    Task<ContentTranslationSource?> GetSourceAsync(ContentEntityType type, int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContentTranslationRow>> GetRowsAsync(ContentEntityType type, IReadOnlyCollection<int> ids,
        string? language, CancellationToken cancellationToken);
}
