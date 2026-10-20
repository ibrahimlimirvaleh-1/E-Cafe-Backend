using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Enums;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public interface IContentTranslationStore
{
    ContentEntityType Type { get; }
    IQueryable<ContentTranslationSource> SourceQuery(int id);
    IQueryable<ContentTranslationRow> RowsQuery(IReadOnlyCollection<int> ids, string? language);
    Task SaveAsync(int id, int languageId, ContentTranslationValue value, long revision, CancellationToken cancellationToken);
    Task DeleteAsync(int id, int languageId, long expectedRevision, CancellationToken cancellationToken);
}
