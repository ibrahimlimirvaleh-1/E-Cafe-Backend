using System.Net;
using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using ECafe.Api.Middlewares;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Redis;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRefreshToken;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;
using Role = ECafe.Domain.Entities.Role;

namespace ECafe.Tests;

public sealed partial class MobileAuthPostgresTests
{
    private const int AuthorizationRoleId = 400001;
    private const int AuthorizationPermissionId = 400002;
    private const int DeniedAuthorizationRoleId = 400003;

    [StockPostgresFact]
    public async Task FreshAuthorizationHasABoundedProjectedReadBudgetWithoutPasswordOrTokenHashes()
    {
        await using var database = await AuthorizationDatabaseAsync();
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var counter = new AuthorizationQueryCounter();
        Assert.True(await CanAuthorizeFromNewScopeAsync(database, cache, observer: counter));
        Assert.InRange(counter.Commands.Count, 1, 4);
        Assert.DoesNotContain(counter.Commands, sql => sql.Contains("password", StringComparison.OrdinalIgnoreCase)
            || sql.Contains("token_hash", StringComparison.OrdinalIgnoreCase));
    }

    [StockPostgresFact]
    public async Task LateParallelCacheFillCannotRestoreCommittedSecurityRevocationsAcrossApiScopes()
    {
        var cache = AuthorizationTestCache();
        using var cacheOwner = cache as IDisposable;
        foreach (var operation in new[] { "deactivate-user", "version-change", "revoke-device",
                     "revoke-membership", "change-role", "revoke-permission" })
        {
            await using var database = await AuthorizationDatabaseAsync();
            var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var publish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            // Köhnə instansiyanın bazadan əvvəl oxuyub commit-dən sonra keşə yazması deterministik qurulur.
            var lateWriter = Task.Run(async () =>
            {
                await using var oldRead = database.Context();
                var users = new UserRepository(oldRead);
                var state = (await users.GetSessionStateAsync(1001))!.Value;
                var active = await new UserRefreshTokenRepository(oldRead)
                    .HasActiveUserSessionAsync(1001, Session, DateTime.UtcNow);
                var role = await new UserRestaurantRepository(oldRead).GetActiveRoleIdAsync(1001, 1001);
                var permissions = await new PermissionCacheService(cache, oldRead,
                    NullLogger<PermissionCacheService>.Instance).GetPermissionsAsync(AuthorizationRoleId);
                captured.TrySetResult();
                await publish.Task.WaitAsync(TimeSpan.FromSeconds(20));
                await cache.SetStringAsync("user-session-state:1001",
                    JsonSerializer.Serialize(new UserSessionState(state.IsActive, state.SessionVersion)));
                await cache.SetStringAsync($"user-session-active:1001:{Session}", active.ToString());
                await cache.SetStringAsync("user-restaurant-access:1001:1001", role!.Value.ToString());
                await cache.SetStringAsync(PermissionCacheKeys.ForRole(AuthorizationRoleId), JsonSerializer.Serialize(permissions));
            });
            try
            {
                await captured.Task.WaitAsync(TimeSpan.FromSeconds(20));
                Assert.True(await CanAuthorizeFromNewScopeAsync(database, cache));
                await using (var write = database.Context(new PermissionCacheInvalidationInterceptor(cache)))
                {
                    if (operation == "revoke-device")
                        await Service(write, AuthorizationSessions(write, cache)).LogoutAsync(new() { RefreshToken = OriginalToken });
                    else
                    {
                        await using var transaction = await write.Database.BeginTransactionAsync();
                        if (operation is "deactivate-user" or "version-change")
                        {
                            var user = await write.Users.SingleAsync(value => value.Id == 1001);
                            if (operation == "deactivate-user") user.IsActive = false;
                            user.SessionVersion++;
                        }
                        else if (operation is "revoke-membership" or "change-role")
                        {
                            var membership = await write.UserRestaurants.SingleAsync(value => value.UserId == 1001 && value.RestaurantId == 1001);
                            if (operation == "revoke-membership") membership.IsActive = false;
                            else membership.RoleId = DeniedAuthorizationRoleId;
                        }
                        else write.RolePermissions.Remove(await write.RolePermissions.SingleAsync(value => value.RoleId == AuthorizationRoleId));
                        await write.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                }
                publish.TrySetResult();
                await lateWriter.WaitAsync(TimeSpan.FromSeconds(20));
                var results = await Task.WhenAll(Enumerable.Range(0, 16)
                    .Select(_ => CanAuthorizeFromNewScopeAsync(database, cache)));
                Assert.All(results, result => Assert.False(result, operation));
                Assert.Equal(0, JsonSerializer.Deserialize<UserSessionState>(
                    (await cache.GetStringAsync("user-session-state:1001"))!)!.SessionVersion);
                Assert.Equal("True", await cache.GetStringAsync($"user-session-active:1001:{Session}"));
                Assert.Equal([AuthorizationPermissionId], JsonSerializer.Deserialize<List<int>>(
                    (await cache.GetStringAsync(PermissionCacheKeys.ForRole(AuthorizationRoleId)))!));
                if (operation is "revoke-membership" or "change-role")
                    Assert.True(await CanAuthorizeFromNewScopeAsync(database, cache, restaurantId: 1002));
                if (operation == "revoke-device")
                    Assert.True(await CanAuthorizeFromNewScopeAsync(database, cache, sessionId: "other-device"));
            }
            finally
            {
                publish.TrySetResult();
                await lateWriter.WaitAsync(TimeSpan.FromSeconds(20));
                foreach (var key in new[] { "user-session-state:1001", $"user-session-active:1001:{Session}",
                             "user-restaurant-access:1001:1001", PermissionCacheKeys.ForRole(AuthorizationRoleId) })
                    await cache.RemoveAsync(key);
            }
        }
    }

    [StockPostgresFact]
    public async Task PermissionEvictionBeforeAnExplicitRollbackCannotChangeTheAuthoritativeGrant()
    {
        await using var database = await AuthorizationDatabaseAsync();
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        await using (var write = database.Context(new PermissionCacheInvalidationInterceptor(cache)))
        {
            await using var transaction = await write.Database.BeginTransactionAsync();
            write.RolePermissions.Remove(await write.RolePermissions.SingleAsync(value => value.RoleId == AuthorizationRoleId));
            await write.SaveChangesAsync();
            Assert.True(await CanAuthorizeFromNewScopeAsync(database, cache));
            await transaction.RollbackAsync();
        }
        Assert.True(await CanAuthorizeFromNewScopeAsync(database, cache));
    }

    /// <summary>Hər yoxlamada ayrıca API scope-u kimi yeni DB bağlantısı, middleware və tenant icazə handler-i yaradır.</summary>
    private static async Task<bool> CanAuthorizeFromNewScopeAsync(Database database, IDistributedCache cache,
        int restaurantId = 1001, string? sessionId = null, IInterceptor? observer = null)
    {
        await using var db = database.Context(observer);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new("userId", "1001"), new("sessionVersion", "0"),
            new("sessionId", sessionId ?? Session), new(ClaimTypes.Role, ((int)RoleCode.Waiter).ToString())], "test"));
        var http = new DefaultHttpContext { User = user };
        http.Request.Path = "/api/v1/authorization-test";
        http.Request.Headers["X-Active-Restaurant-Id"] = restaurantId.ToString();
        var passed = false;
        var middleware = new ActiveUserMiddleware(_ => { passed = true; return Task.CompletedTask; });
        try { await middleware.Invoke(http, AuthorizationSessions(db, cache)); }
        catch (UnauthorizedException) { return false; }
        Assert.True(passed);
        var requirement = new PermissionRequirement(AuthorizationPermissionId);
        var authorization = new AuthorizationHandlerContext([requirement], user, http);
        var handler = new PermissionAuthorizationHandler(new PermissionCacheService(cache, db,
                NullLogger<PermissionCacheService>.Instance), new HttpContextAccessor { HttpContext = http },
            new UserRestaurantAccessCache(cache, new UserRestaurantRepository(db), new ConfigurationBuilder().Build(),
                NullLogger<UserRestaurantAccessCache>.Instance));
        await handler.HandleAsync(authorization);
        Assert.Empty(db.ChangeTracker.Entries());
        return authorization.HasSucceeded;
    }

    private sealed class AuthorizationQueryCounter : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        /// <summary>Keş əvəzinə baza oxunuşunun sorğu sayını və yalnız lazımi sütunları seçdiyini ölçür; SQL loqa çıxarılmır.</summary>
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>Real PostgreSQL repository-ləri ilə mövcud sessiya interfeysini test edir; məxfi tətbiq ayarlarını oxumur.</summary>
    private static UserSessionStateCache AuthorizationSessions(ECafeDbContext db, IDistributedCache cache)
        => new(cache, new UserRepository(db), new UserRefreshTokenRepository(db), new ConfigurationBuilder().Build(),
            NullLogger<UserSessionStateCache>.Instance);

    /// <summary>Yalnız GUID-li test bazasında bir istifadəçiyə iki restoran aidiyyəti və ayrıca sintetik icazə verir.</summary>
    private static async Task<Database> AuthorizationDatabaseAsync()
    {
        var database = await Database.CreateAsync();
        try
        {
            await using var db = database.Context();
            db.Roles.AddRange(new Role { Id = AuthorizationRoleId, Name = "Authorization test role" },
                new Role { Id = DeniedAuthorizationRoleId, Name = "Denied authorization test role" });
            db.Permissions.Add(new Permission { Id = AuthorizationPermissionId, Name = "Authorization consistency test" });
            db.RolePermissions.Add(new RolePermission { RoleId = AuthorizationRoleId, PermissionId = AuthorizationPermissionId });
            var membership = await db.UserRestaurants.SingleAsync(value => value.UserId == 1001 && value.RestaurantId == 1001);
            membership.IsActive = true;
            membership.RoleId = AuthorizationRoleId;
            db.Restaurants.Add(new Restaurant { Id = 1002, Name = "Other authorization tenant", Location = "Baku", Phone = "1002", IsActive = true });
            db.UserRestaurants.Add(new UserRestaurant { UserId = 1001, RestaurantId = 1002, RoleId = AuthorizationRoleId, IsActive = true });
            await db.SaveChangesAsync();
            return database;
        }
        catch { await database.DisposeAsync(); throw; }
    }

    /// <summary>Açıq seçilmiş loopback Redis-dən istifadə edir; Redis verilmədikdə yarış ssenarisini yaddaş keşində saxlayır.</summary>
    private static IDistributedCache AuthorizationTestCache()
    {
        var connection = Environment.GetEnvironmentVariable("ECAFE_TEST_REDIS");
        if (string.IsNullOrWhiteSpace(connection))
            return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var settings = ConfigurationOptions.Parse(connection);
        foreach (var endpoint in settings.EndPoints)
        {
            var host = endpoint switch { DnsEndPoint dns => dns.Host, IPEndPoint ip => ip.Address.ToString(), _ => "" };
            if (host != "localhost" && (!IPAddress.TryParse(host, out var address) || !IPAddress.IsLoopback(address)))
                throw new InvalidOperationException("Authorization tests require isolated loopback Redis.");
        }
        return new RedisCache(Options.Create(new RedisCacheOptions { ConfigurationOptions = settings }));
    }
}
