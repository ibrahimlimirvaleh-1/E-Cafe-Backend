using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;
using Role = ECafe.Domain.Entities.Role;

namespace ECafe.Tests;

public sealed class PermissionCacheFallbackTests
{
    private const int TestRoleId = 400001;
    private const int TestPermissionId = 400002;

    [Theory]
    [InlineData("connection")]
    [InlineData("timeout")]
    [InlineData("redis-timeout")]
    [InlineData("io")]
    [InlineData("socket")]
    [InlineData("server")]
    public async Task PermissionChecksDoNotReadOrWriteRedisEvenWhenItIsUnavailable(string failure)
    {
        await using var db = await DatabaseAsync();
        var cache = new Mock<IDistributedCache>(MockBehavior.Strict);
        cache.Setup(value => value.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CacheFailure(failure));
        cache.Setup(value => value.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(CacheFailure(failure));
        var service = Service(cache.Object, db);
        Assert.Equal([TestPermissionId], await service.GetPermissionsAsync(TestRoleId));
        db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(value => value.RoleId == TestRoleId));
        await db.SaveChangesAsync();
        Assert.Empty(await service.GetPermissionsAsync(TestRoleId));
        cache.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("timeout")]
    [InlineData("redis-timeout")]
    [InlineData("io")]
    [InlineData("socket")]
    [InlineData("server")]
    public async Task FailedEvictionDoesNotRestoreARevokedGrantOrLeakProviderDetails(string failure)
    {
        await using var db = await DatabaseAsync();
        var cache = new Mock<IDistributedCache>(MockBehavior.Strict);
        cache.Setup(value => value.RemoveAsync(PermissionCacheKeys.ForRole(TestRoleId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CacheFailure(failure));
        var logger = new RecordingLogger();
        var service = new PermissionCacheService(cache.Object, db, logger);
        Assert.Equal([TestPermissionId], await service.GetPermissionsAsync(TestRoleId));
        db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(value => value.RoleId == TestRoleId));
        await db.SaveChangesAsync();
        await service.InvalidateAsync(TestRoleId);
        Assert.Empty(await service.GetPermissionsAsync(TestRoleId));
        cache.Verify(value => value.RemoveAsync(PermissionCacheKeys.ForRole(TestRoleId), It.IsAny<CancellationToken>()), Times.Once);
        cache.VerifyNoOtherCalls();
        Assert.Single(logger.Messages);
        Assert.DoesNotContain("sensitive-test", logger.Messages[0]);
        Assert.All(logger.Exceptions, Assert.Null);
    }

    [Theory]
    [InlineData("[400002]")]
    [InlineData("[]")]
    [InlineData("invalid-json")]
    [InlineData("null")]
    [InlineData("{\"wrongShape\":true}")]
    public async Task LegacyPayloadCannotOverrideTheCurrentDatabaseDecision(string payload)
    {
        await using var db = await DatabaseAsync();
        var cache = new Mock<IDistributedCache>(MockBehavior.Strict);
        cache.Setup(value => value.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(payload));
        var service = Service(cache.Object, db);
        Assert.Equal([TestPermissionId], await service.GetPermissionsAsync(TestRoleId));
        db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(value => value.RoleId == TestRoleId));
        await db.SaveChangesAsync();
        Assert.Empty(await service.GetPermissionsAsync(TestRoleId));
        cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DatabaseFailureWithAWarmAllowCacheIsNotConvertedToPermissionSuccess()
    {
        var db = await DatabaseAsync();
        await db.DisposeAsync();
        var cache = new Mock<IDistributedCache>(MockBehavior.Strict);
        cache.Setup(value => value.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("[400002]"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => Service(cache.Object, db).GetPermissionsAsync(TestRoleId));
        cache.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndProgrammingErrorsAreNotTreatedAsEvictionOutages(bool cancelled)
    {
        await using var db = await DatabaseAsync();
        var cache = new Mock<IDistributedCache>(MockBehavior.Strict);
        Exception error = cancelled ? new OperationCanceledException() : new InvalidOperationException("Programming failure");
        cache.Setup(value => value.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(error);
        Assert.Same(error, await Record.ExceptionAsync(() => Service(cache.Object, db).InvalidateAsync(TestRoleId)));
    }

    [Fact]
    public async Task UnknownRolesAndSoftDeletedGrantsRemainDeniedWithoutRedis()
    {
        await using var db = await DatabaseAsync();
        db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(value => value.RoleId == TestRoleId));
        await db.SaveChangesAsync();
        var cache = new Mock<IDistributedCache>(MockBehavior.Strict);
        var service = Service(cache.Object, db);
        Assert.Empty(await service.GetPermissionsAsync(TestRoleId));
        Assert.Empty(await service.GetPermissionsAsync(int.MaxValue));
        cache.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RestaurantScopeAndRequestedPermissionAreStillRequired(bool assigned, bool granted)
    {
        await using var db = await DatabaseAsync();
        var cache = new Mock<IDistributedCache>(MockBehavior.Strict);
        var access = new Mock<IUserRestaurantAccessCache>(MockBehavior.Strict);
        access.Setup(value => value.GetActiveRoleIdAsync(17, 5)).ReturnsAsync(assigned ? TestRoleId : (int?)null);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new("userId", "17"),
            new(ClaimTypes.Role, ((int)RoleCode.Waiter).ToString())], "test"));
        var http = new DefaultHttpContext { User = user };
        http.Request.Headers["X-Active-Restaurant-Id"] = "5";
        var requirement = new PermissionRequirement(granted ? TestPermissionId : int.MaxValue);
        var context = new AuthorizationHandlerContext([requirement], user, http);
        var handler = new PermissionAuthorizationHandler(Service(cache.Object, db),
            new HttpContextAccessor { HttpContext = http }, access.Object);
        await handler.HandleAsync(context);
        Assert.Equal(assigned && granted, context.HasSucceeded);
        cache.VerifyNoOtherCalls();
    }

    /// <summary>Hər sınağa ayrıca baza və aktiv icazə verir; real PostgreSQL və istifadəçi məlumatı işlədilmir.</summary>
    private static async Task<ECafeDbContext> DatabaseAsync()
    {
        var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.Database.EnsureCreatedAsync();
        db.RolePermissions.Add(new RolePermission
        {
            Id = TestRoleId, Role = new Role { Id = TestRoleId, Name = "Consistency test role" },
            Permission = new Permission { Id = TestPermissionId, Name = "Consistency test permission" }
        });
        await db.SaveChangesAsync();
        return db;
    }

    /// <summary>İstehsal xidmətini yalnız test bazası və verilən keş ilə yaradır.</summary>
    private static PermissionCacheService Service(IDistributedCache cache, ECafeDbContext db)
        => new(cache, db, NullLogger<PermissionCacheService>.Instance);

    /// <summary>Keşdən gələ bilən xətaları məxfi test mətni ilə modelləşdirərək loqa sızmanı da yoxlayır.</summary>
    private static Exception CacheFailure(string kind) => kind switch
    {
        "connection" => new RedisConnectionException(ConnectionFailureType.UnableToConnect, "sensitive-test connection"),
        "timeout" => new TimeoutException("sensitive-test timeout"),
        "redis-timeout" => new RedisTimeoutException("sensitive-test timeout", CommandStatus.Unknown),
        "io" => new IOException("sensitive-test io"),
        "socket" => new SocketException((int)SocketError.ConnectionRefused),
        "server" => new RedisServerException("sensitive-test NOPERM"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private sealed class RecordingLogger : ILogger<PermissionCacheService>
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];
        /// <summary>Test loqları üçün ayrıca scope resursu saxlamır.</summary>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <summary>Təmizləmə xəbərdarlıqlarını yoxlamaq üçün test loqlarını açıq saxlayır.</summary>
        public bool IsEnabled(LogLevel level) => true;
        /// <summary>Yalnız yaddaşa yazaraq məxfi xəta məzmununun loqa ötürülmədiyini yoxlayır.</summary>
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? error,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, error));
            Exceptions.Add(error);
        }
    }
}
