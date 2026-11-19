using ECafe.Domain.Entities;
using ECafe.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECafe.Infrastructure.Authorization;

public sealed class PermissionCacheInvalidationInterceptor : SaveChangesInterceptor
{
    private readonly AuthorizationCacheInvalidator _invalidator;
    private readonly HashSet<int> _pendingRoleIds = [];

    /// <summary>Köhnə Redis açarlarının təmizlənməsini saxlayır; icazə qərarları artıq həmin açarlardan asılı deyil.</summary>
    public PermissionCacheInvalidationInterceptor(IDistributedCache cache,
        ILogger<PermissionCacheInvalidationInterceptor>? logger = null)
    {
        _invalidator = new(cache, logger ?? NullLogger<PermissionCacheInvalidationInterceptor>.Instance);
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

    /// <summary>Dəyişən aidiyyətlərin əvvəlki və yeni rollarını köhnə açarların təmizlənməsi üçün toplayır.</summary>
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

    /// <summary>İcazə başqa rola köçürüləndə həm köhnə, həm yeni rolun açarını seçir.</summary>
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

    /// <summary>Köhnə rol açarlarını təmizləyir; Redis nasazlığı saxlanmış DB dəyişikliyini uğursuz kimi göstərmir.</summary>
    private void InvalidateCapturedRoleIds()
    {
        try
        {
            foreach (var roleId in _pendingRoleIds)
                _invalidator.Invalidate(PermissionCacheKeys.ForRole(roleId));
        }
        finally { _pendingRoleIds.Clear(); }
    }

    /// <summary>Köhnə rol açarlarını asinxron təmizləyir; provayder xətasını ayırır və ləğvetmə tokenini saxlayır.</summary>
    private async Task InvalidateCapturedRoleIdsAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var roleId in _pendingRoleIds)
                await _invalidator.InvalidateAsync(PermissionCacheKeys.ForRole(roleId), cancellationToken);
        }
        finally { _pendingRoleIds.Clear(); }
    }

}
