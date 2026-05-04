using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ECafe.Infrastructure.Redis
{
    public class PermissionCacheService : IPermissionCacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ECafeDbContext _context;

        public PermissionCacheService(
            IDistributedCache cache,
            ECafeDbContext context)
        {
            _cache = cache;
            _context = context;
        }

        private string Key(int roleId) => $"role_permissions_{roleId}";

        public async Task<List<int>> GetPermissionsAsync(int roleId)
        {
            var cacheKey = Key(roleId);

            var cached = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cached))
            {
                return JsonSerializer.Deserialize<List<int>>(cached)!;
            }

            var permissions = await _context.RolePermissions
                .Where(x => x.RoleId == roleId)
                .Select(x => x.Permission.Id)
                .ToListAsync();

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(permissions),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                });

            return permissions;
        }

        public async Task InvalidateAsync(int roleId)
        {
            await _cache.RemoveAsync(Key(roleId));
        }
    }
}
