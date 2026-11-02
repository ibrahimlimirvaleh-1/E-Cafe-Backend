using ECafe.Domain.Entities;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECafe.Tests;

public sealed class PermissionCacheInvalidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevokingPermissionRemovesWarmVersionedCache(bool asynchronous)
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        await using var db = Context(cache);
        await db.Database.EnsureCreatedAsync();
        var permission = new Permission { Id = 100001, Name = "Audit-only permission" };
        var role = new Role { Id = 100001, Name = "Audit role" };
        var assignment = new RolePermission { Id = 100001, RoleId = role.Id, Role = role, PermissionId = permission.Id, Permission = permission };
        db.RolePermissions.Add(assignment); await db.SaveChangesAsync();
        var service = new PermissionCacheService(cache, db);
        Assert.Contains(permission.Id, await service.GetPermissionsAsync(role.Id));
        Assert.NotNull(await cache.GetStringAsync("role_permissions_v3_100001"));
        db.RolePermissions.Remove(assignment);
        if (asynchronous) await db.SaveChangesAsync(); else db.SaveChanges();
        Assert.Null(await cache.GetStringAsync("role_permissions_v3_100001"));
        Assert.Empty(await service.GetPermissionsAsync(role.Id));
    }

    [Fact]
    public async Task ReassigningPermissionInvalidatesBothOldAndNewRoles()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        await using var db = Context(cache);
        await db.Database.EnsureCreatedAsync();
        var first = new Role { Id = 100001, Name = "First" };
        var second = new Role { Id = 100002, Name = "Second" };
        var permission = new Permission { Id = 100001, Name = "Test permission" };
        var assignment = new RolePermission { Id = 100001, RoleId = first.Id, Role = first, PermissionId = permission.Id, Permission = permission };
        db.Roles.Add(second); db.RolePermissions.Add(assignment); await db.SaveChangesAsync();
        var service = new PermissionCacheService(cache, db);
        Assert.Contains(permission.Id, await service.GetPermissionsAsync(first.Id));
        Assert.Empty(await service.GetPermissionsAsync(second.Id));
        db.RolePermissions.Remove(assignment);
        db.RolePermissions.Add(new RolePermission { Id = 100002, RoleId = second.Id, Role = second, PermissionId = permission.Id, Permission = permission });
        await db.SaveChangesAsync();
        Assert.Null(await cache.GetStringAsync(PermissionCacheKeys.ForRole(first.Id)));
        Assert.Null(await cache.GetStringAsync(PermissionCacheKeys.ForRole(second.Id)));
        Assert.Empty(await service.GetPermissionsAsync(first.Id));
        Assert.Contains(permission.Id, await service.GetPermissionsAsync(second.Id));
        await service.InvalidateAsync(second.Id);
        Assert.Null(await cache.GetStringAsync(PermissionCacheKeys.ForRole(second.Id)));
    }

    /// <summary>Hər sınaq üçün ayrıca InMemory baza və real keş interceptor-u yaradır; server Redis və tətbiq bazasına toxunmur.</summary>
    private static ECafeDbContext Context(IDistributedCache cache)
        => new(new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new PermissionCacheInvalidationInterceptor(cache)).Options);
}
