using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ECafe.Infrastructure.Redis;

public sealed class PermissionCacheService : IPermissionCacheService
{
    private readonly ECafeDbContext _context;
    private readonly AuthorizationCacheInvalidator _invalidator;

    /// <summary>Mövcud DI müqaviləsini və köhnə açarların təhlükəsiz təmizlənməsini saxlayır.</summary>
    public PermissionCacheService(IDistributedCache cache, ECafeDbContext context, ILogger<PermissionCacheService> logger)
    {
        _context = context;
        _invalidator = new(cache, logger);
    }

    /// <summary>Hər qərar üçün cari icazələri bazadan oxuyur; köhnə Redis cavabı və gecikmiş yazı ləğv edilmiş icazəni bərpa etmir.</summary>
    public Task<List<int>> GetPermissionsAsync(int roleId)
        => _context.RolePermissions.AsNoTracking()
            .Where(row => row.RoleId == roleId)
            .Select(row => row.Permission.Id)
            .ToListAsync();

    /// <summary>Əvvəlki versiyalardan qalan açarı təmizləyir; bazadan yoxlama provayderin işləməsindən asılı deyil.</summary>
    public Task InvalidateAsync(int roleId)
        => _invalidator.InvalidateAsync(PermissionCacheKeys.ForRole(roleId));
}
