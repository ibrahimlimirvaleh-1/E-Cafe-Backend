using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;

namespace ECafe.Infrastructure.Authorization;

public sealed class PermissionCacheInvalidationInterceptor : SaveChangesInterceptor
{
    private readonly IDistributedCache _cache;
    private readonly HashSet<int> _pendingRoleIds = [];

    public PermissionCacheInvalidationInterceptor(IDistributedCache cache)
    {
        _cache = cache;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureChangedRoleIds(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureChangedRoleIds(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        InvalidateCapturedRoleIds();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await InvalidateCapturedRoleIdsAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendingRoleIds.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pendingRoleIds.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void CaptureChangedRoleIds(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<RolePermission>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            foreach (var roleId in GetRoleIds(entry))
            {
                if (roleId > 0)
                {
                    _pendingRoleIds.Add(roleId);
                }
            }
        }
    }

    private static IEnumerable<int> GetRoleIds(EntityEntry<RolePermission> entry)
    {
        yield return entry.Entity.RoleId;

        if (entry.State is EntityState.Modified or EntityState.Deleted)
        {
            var originalRoleId = entry.Property(x => x.RoleId).OriginalValue;
            if (originalRoleId != entry.Entity.RoleId)
            {
                yield return originalRoleId;
            }
        }
    }

    private void InvalidateCapturedRoleIds()
    {
        foreach (var roleId in _pendingRoleIds)
        {
            _cache.Remove(PermissionCacheKey(roleId));
        }

        _pendingRoleIds.Clear();
    }

    private async Task InvalidateCapturedRoleIdsAsync(CancellationToken cancellationToken)
    {
        foreach (var roleId in _pendingRoleIds)
        {
            await _cache.RemoveAsync(PermissionCacheKey(roleId), cancellationToken);
        }

        _pendingRoleIds.Clear();
    }

    private static string PermissionCacheKey(int roleId) => $"role_permissions_{roleId}";
}
