using ECafe.Domain.Entities;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Moq;
using StackExchange.Redis;
using Role = ECafe.Domain.Entities.Role;

namespace ECafe.Tests;

public sealed class PermissionCacheInvalidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RedisEvictionFailureCannotUndoACommittedGrantRevocation(bool asynchronous)
    {
        var cache = new Mock<IDistributedCache>(MockBehavior.Strict);
        cache.Setup(value => value.Remove(It.IsAny<string>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Synthetic offline Redis"));
        cache.Setup(value => value.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Synthetic offline Redis"));
        await using var db = Context(cache.Object);
        await db.Database.EnsureCreatedAsync();
        var grant = new RolePermission
        {
            Id = 910001, Role = new ECafe.Domain.Entities.Role { Id = 910001, Name = "Offline eviction test role" },
            Permission = new Permission { Id = 910002, Name = "Offline eviction test permission" }
        };
        db.RolePermissions.Add(grant);
        await db.SaveChangesAsync();
        var service = new PermissionCacheService(cache.Object, db, NullLogger<PermissionCacheService>.Instance);
        Assert.Equal([910002], await service.GetPermissionsAsync(910001));
        db.RolePermissions.Remove(grant);
        if (asynchronous) await db.SaveChangesAsync(); else db.SaveChanges();
        Assert.Empty(await service.GetPermissionsAsync(910001));
        Assert.True((await db.RolePermissions.IgnoreQueryFilters().SingleAsync(value => value.RoleId == 910001)).IsDeleted);
    }

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
        var service = new PermissionCacheService(cache, db, NullLogger<PermissionCacheService>.Instance);
        Assert.Contains(permission.Id, await service.GetPermissionsAsync(role.Id));
        await cache.SetStringAsync("role_permissions_v3_100001", "[100001]");
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
        var service = new PermissionCacheService(cache, db, NullLogger<PermissionCacheService>.Instance);
        Assert.Contains(permission.Id, await service.GetPermissionsAsync(first.Id));
        Assert.Empty(await service.GetPermissionsAsync(second.Id));
        await cache.SetStringAsync(PermissionCacheKeys.ForRole(first.Id), "[100001]");
        await cache.SetStringAsync(PermissionCacheKeys.ForRole(second.Id), "[]");
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
