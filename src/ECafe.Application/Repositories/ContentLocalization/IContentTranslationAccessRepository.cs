using ECafe.Domain.Enums;

namespace ECafe.Application.Repositories.ContentLocalization;

public interface IContentTranslationAccessRepository
{
    Task<ContentActor?> GetActorAsync(int userId, CancellationToken cancellationToken);
    Task<bool> HasActiveSessionAsync(int userId, string sessionId, CancellationToken cancellationToken);
    Task<bool> HasPermissionAsync(int roleId, PermissionCode permission, CancellationToken cancellationToken);
}
