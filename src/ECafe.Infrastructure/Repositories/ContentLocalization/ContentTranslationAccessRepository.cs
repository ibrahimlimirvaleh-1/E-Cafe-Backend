using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public sealed class ContentTranslationAccessRepository(ECafeDbContext context) : IContentTranslationAccessRepository
{
    /// <summary>Aktiv istifadəçini və qüvvədə olan restoran təyinatlarını tək sorğu ilə oxuyur.</summary>
    public async Task<ContentActor?> GetActorAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await context.Users.AsNoTracking().Where(user => user.Id == userId && user.IsActive && !user.IsDeleted)
            .Select(user => new
            {
                user.RoleId, user.SessionVersion,
                Assignments = user.UserRestaurants.Where(assignment => assignment.IsActive && !assignment.IsDeleted && !assignment.Restaurant.IsDeleted)
                    .Select(assignment => new ContentAssignment(assignment.RestaurantId, assignment.RoleId)).ToList()
            }).SingleOrDefaultAsync(cancellationToken);
        return user == null ? null : new ContentActor(user.RoleId, user.SessionVersion, user.Assignments);
    }

    /// <summary>Cihaz sessiyasının istifadəçiyə aid, silinməmiş, ləğv edilməmiş və UTC vaxtına görə qüvvədə olduğunu keşsiz yoxlayır.</summary>
    public Task<bool> HasActiveSessionAsync(int userId, string sessionId, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        return context.UserRefreshTokens.AsNoTracking().AnyAsync(token => token.UserId == userId && token.SessionId == sessionId &&
            !token.IsDeleted && token.RevokedAt == null && token.ExpiresAt > nowUtc && token.User.IsActive && !token.User.IsDeleted,
            cancellationToken);
    }

    /// <summary>Rolun mənbə CRUD icazəsini canlı bazadan yoxlayır; silinmiş əlaqələr giriş hüququ vermir.</summary>
    public Task<bool> HasPermissionAsync(int roleId, PermissionCode permission, CancellationToken cancellationToken)
        => context.RolePermissions.AsNoTracking().AnyAsync(row => row.RoleId == roleId && row.PermissionId == (int)permission &&
            !row.IsDeleted && !row.Role.IsDeleted && !row.Permission.IsDeleted, cancellationToken);
}
