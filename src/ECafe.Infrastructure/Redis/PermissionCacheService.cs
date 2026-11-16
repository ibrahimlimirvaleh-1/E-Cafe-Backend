using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ECafe.Infrastructure.Redis
{
    public class PermissionCacheService : IPermissionCacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ECafeDbContext _context;
        private readonly ILogger<PermissionCacheService> _logger;

        public PermissionCacheService(
            IDistributedCache cache,
            ECafeDbContext context,
            ILogger<PermissionCacheService> logger)
        {
            _cache = cache;
            _context = context;
            _logger = logger;
        }

        /// <summary>Redis oxuma xətasında icazələri bazadan yoxlayır; baza xətasını və ləğvetməni gizlədərək icazə vermir.</summary>
        public async Task<List<int>> GetPermissionsAsync(int roleId)
        {
            var cacheKey = PermissionCacheKeys.ForRole(roleId);
            var cacheAvailable = true;

            try
            {
                var cached = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrWhiteSpace(cached) && JsonSerializer.Deserialize<List<int>>(cached) is { } cachedPermissions)
                    return cachedPermissions;
            }
            catch (JsonException)
            {
                _logger.LogWarning("Role permission cache payload is invalid for role {RoleId}; using database.", roleId);
            }
            catch (Exception error) when (IsCacheFailure(error))
            {
                cacheAvailable = false;
                _logger.LogWarning("Role permission cache read unavailable for role {RoleId} ({ErrorType}); using database.",
                    roleId, error.GetType().Name);
            }

            var permissions = await _context.RolePermissions
                .Where(x => x.RoleId == roleId)
                .Select(x => x.Permission.Id)
                .ToListAsync();

            if (cacheAvailable)
            {
                try
                {
                    await _cache.SetStringAsync(
                        cacheKey,
                        JsonSerializer.Serialize(permissions),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                        });
                }
                catch (Exception error) when (IsCacheFailure(error))
                {
                    _logger.LogWarning("Role permission cache write unavailable for role {RoleId} ({ErrorType}); using database result.",
                        roleId, error.GetType().Name);
                }
            }

            return permissions;
        }

        /// <summary>Mövcud ləğvetmə qorumasını saxlayır; silmə alınmadıqda uğurlu invalidation kimi göstərmir.</summary>
        public async Task InvalidateAsync(int roleId)
        {
            await _cache.RemoveAsync(PermissionCacheKeys.ForRole(roleId));
        }

        /// <summary>Keş sərhədində provayder xətalarını ayırır; ləğvetmə, proqramlaşdırma və resurs çatışmazlığı gizlədilmir.</summary>
        private static bool IsCacheFailure(Exception error)
            => error is not (OperationCanceledException or ArgumentException or InvalidOperationException or
                NotSupportedException or OutOfMemoryException);
    }
}
