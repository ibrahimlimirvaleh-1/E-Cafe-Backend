using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Domain.Enums;

namespace ECafe.Application.Repositories.ContentLocalization;

public sealed record ContentTranslationSource(int Id, int? RestaurantId, ContentTranslationValue Canonical);
public sealed record ContentTranslationRow(int Id, string LanguageCode, long Revision, ContentTranslationValue Value);
public sealed record ContentLanguage(int Id, string Code, bool IsActive, string? Name = null, bool IsDefault = false);
public sealed record ContentActor(int RoleId, int SessionVersion, IReadOnlyList<ContentAssignment> Assignments);
public sealed record ContentAssignment(int RestaurantId, int RoleId);

public interface IContentTranslationRepository : IContentTranslationQuery
{
    Task AcquireLockAsync(ContentEntityType type, int id, CancellationToken cancellationToken);
    Task SaveAsync(ContentEntityType type, int id, int languageId, ContentTranslationValue value,
        long revision, CancellationToken cancellationToken);
    Task DeleteAsync(ContentEntityType type, int id, int languageId, long expectedRevision, CancellationToken cancellationToken);
}
