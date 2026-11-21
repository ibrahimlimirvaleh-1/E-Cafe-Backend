using System.Security.Claims;
using System.Text.Json;
using ECafe.Api.Middlewares;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Redis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class AuthorizationStateConsistencyTests
{
    [Theory]
    [InlineData(false, 7, true)]
    [InlineData(true, 8, true)]
    [InlineData(true, 7, false)]
    public async Task WarmAllowCacheCannotBypassDeactivationVersionChangeOrDeviceRevocation(
        bool isActive, int version, bool deviceActive)
    {
        var cache = Cache();
        var oldState = JsonSerializer.Serialize(new UserSessionState(true, 7));
        await cache.SetStringAsync("user-session-state:17", oldState);
        await cache.SetStringAsync("user-session-active:17:device-a", "True");
        (bool IsActive, int SessionVersion, bool HasRequiredStaffProfile)? state = (true, 7, true);
        var activeSession = true;
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(value => value.GetSessionStateAsync(17)).ReturnsAsync(() => state);
        var tokens = new Mock<IUserRefreshTokenRepository>(MockBehavior.Strict);
        tokens.Setup(value => value.HasActiveUserSessionAsync(17, "device-a", It.IsAny<DateTime>()))
            .ReturnsAsync(() => activeSession);
        var service = Sessions(cache, users.Object, tokens.Object);
        var reachedNext = false;
        var middleware = new ActiveUserMiddleware(_ => { reachedNext = true; return Task.CompletedTask; });
        await middleware.Invoke(Request(), service);
        Assert.True(reachedNext);

        state = (isActive, version, true);
        activeSession = deviceActive;
        reachedNext = false;
        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => middleware.Invoke(Request(), service));
        Assert.Equal(isActive ? ErrorCode.SessionInvalid : ErrorCode.UserDeactivated, error.Code);
        Assert.False(reachedNext);
        Assert.Equal(oldState, await cache.GetStringAsync("user-session-state:17"));
        Assert.Equal("True", await cache.GetStringAsync("user-session-active:17:device-a"));
    }

    [Fact]
    public async Task DeletedUserCannotUseAWarmSessionState()
    {
        var cache = Cache();
        await cache.SetStringAsync("user-session-state:17", JsonSerializer.Serialize(new UserSessionState(true, 7)));
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(value => value.GetSessionStateAsync(17)).ReturnsAsync(((bool, int, bool)?)null);
        var middleware = new ActiveUserMiddleware(_ => throw new InvalidOperationException("Must not reach endpoint"));
        await Assert.ThrowsAsync<UnauthorizedException>(() => middleware.Invoke(Request(),
            Sessions(cache, users.Object, Mock.Of<IUserRefreshTokenRepository>())));
    }

    [Fact]
    public async Task MissingStaffProfileRejectsEvenAnOldCustomerClaimAndWarmAllowCache()
    {
        var cache = Cache();
        await cache.SetStringAsync("user-session-state:17", JsonSerializer.Serialize(new UserSessionState(true, 7)));
        var users = new Mock<IUserRepository>();
        users.Setup(value => value.GetSessionStateAsync(17)).ReturnsAsync((true, 7, false));
        var http = Request();
        http.User = new(new ClaimsIdentity([new("userId", "17"), new("sessionVersion", "7"),
            new(ClaimTypes.Role, "5")], "test"));
        var middleware = new ActiveUserMiddleware(_ => throw new InvalidOperationException("Must not reach endpoint"));
        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => middleware.Invoke(http,
            Sessions(cache, users.Object, Mock.Of<IUserRefreshTokenRepository>())));
        Assert.Equal(ErrorCode.StaffProfileInactive, error.Code);
    }

    [Fact]
    public async Task RestaurantRoleChangesAndRevocationDoNotWaitForCacheExpiry()
    {
        var cache = Cache();
        await cache.SetStringAsync("user-restaurant-access:17:5", "1");
        int? role = 4;
        var repository = new Mock<IUserRestaurantRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetActiveRoleIdAsync(17, 5)).ReturnsAsync(() => role);
        var service = Membership(cache, repository.Object);
        Assert.Equal(4, await service.GetActiveRoleIdAsync(17, 5));
        role = 5;
        Assert.Equal(5, await service.GetActiveRoleIdAsync(17, 5));
        role = null;
        Assert.Null(await service.GetActiveRoleIdAsync(17, 5));
        Assert.Equal("1", await cache.GetStringAsync("user-restaurant-access:17:5"));
        repository.Verify(value => value.GetActiveRoleIdAsync(17, 5), Times.Exactly(3));
    }

    [Fact]
    public async Task StaleDenialDoesNotHideANewlyGrantedMembership()
    {
        var cache = Cache();
        await cache.SetStringAsync("user-restaurant-access:17:5", "0");
        var repository = new Mock<IUserRestaurantRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetActiveRoleIdAsync(17, 5)).ReturnsAsync(4);
        repository.Setup(value => value.GetActiveRoleIdAsync(17, 6)).ReturnsAsync((int?)null);
        var service = Membership(cache, repository.Object);
        Assert.Equal(4, await service.GetActiveRoleIdAsync(17, 5));
        Assert.Null(await service.GetActiveRoleIdAsync(17, 6));
    }

    [Fact]
    public async Task DeviceExpiryAndOtherDeviceIdentityAreReadFromTheDatabase()
    {
        var expiry = DateTime.UtcNow.AddHours(1);
        var tokens = new Mock<IUserRefreshTokenRepository>(MockBehavior.Strict);
        tokens.Setup(value => value.HasActiveUserSessionAsync(17, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((int _, string session, DateTime now) => session == "device-a" && now < expiry);
        var cache = Cache();
        await cache.SetStringAsync("user-session-active:17:device-a", "True");
        var service = Sessions(cache, Mock.Of<IUserRepository>(), tokens.Object);
        Assert.True(await service.IsSessionActiveAsync(17, "device-a"));
        Assert.False(await service.IsSessionActiveAsync(17, "other-device"));
        expiry = DateTime.UtcNow.AddMinutes(-1);
        Assert.False(await service.IsSessionActiveAsync(17, "device-a"));
    }

    [Theory]
    [InlineData("user")]
    [InlineData("device")]
    [InlineData("restaurant")]
    public async Task DatabaseFailureDoesNotUseTheOldAllowCache(string target)
    {
        var cache = Cache();
        await cache.SetStringAsync("user-session-state:17", JsonSerializer.Serialize(new UserSessionState(true, 7)));
        await cache.SetStringAsync("user-session-active:17:device-a", "True");
        await cache.SetStringAsync("user-restaurant-access:17:5", "4");
        var error = new TimeoutException("Synthetic database failure");
        var users = new Mock<IUserRepository>();
        users.Setup(value => value.GetSessionStateAsync(17)).ThrowsAsync(error);
        var tokens = new Mock<IUserRefreshTokenRepository>();
        tokens.Setup(value => value.HasActiveUserSessionAsync(17, "device-a", It.IsAny<DateTime>())).ThrowsAsync(error);
        var restaurants = new Mock<IUserRestaurantRepository>();
        restaurants.Setup(value => value.GetActiveRoleIdAsync(17, 5)).ThrowsAsync(error);
        var sessions = Sessions(cache, users.Object, tokens.Object);
        // Baza xətasının hər təhlükəsizlik oxuma yolunda dəyişmədən yayıldığını yoxlayır.
        Task Check() => target switch
        {
            "user" => sessions.GetAsync(17),
            "device" => sessions.IsSessionActiveAsync(17, "device-a"),
            _ => Membership(cache, restaurants.Object).GetActiveRoleIdAsync(17, 5)
        };
        Assert.Same(error, await Record.ExceptionAsync(Check));
    }

    [Fact]
    public async Task SlowRedisCannotDelaySecurityReads()
    {
        var cache = new Mock<IDistributedCache>(MockBehavior.Strict);
        cache.Setup(value => value.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource<byte[]?>().Task);
        var users = new Mock<IUserRepository>();
        users.Setup(value => value.GetSessionStateAsync(17)).ReturnsAsync((true, 7, true));
        var tokens = new Mock<IUserRefreshTokenRepository>();
        tokens.Setup(value => value.HasActiveUserSessionAsync(17, "device-a", It.IsAny<DateTime>())).ReturnsAsync(false);
        var restaurants = new Mock<IUserRestaurantRepository>();
        restaurants.Setup(value => value.GetActiveRoleIdAsync(17, 5)).ReturnsAsync((int?)null);
        var sessions = Sessions(cache.Object, users.Object, tokens.Object);
        Assert.Equal(7, (await sessions.GetAsync(17).WaitAsync(TimeSpan.FromSeconds(5)))!.SessionVersion);
        Assert.False(await sessions.IsSessionActiveAsync(17, "device-a").WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Null(await Membership(cache.Object, restaurants.Object).GetActiveRoleIdAsync(17, 5).WaitAsync(TimeSpan.FromSeconds(5)));
        cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LegacyCleanupDoesNotTouchOtherDevicesOrRestaurants()
    {
        var cache = Cache();
        string[] keys = ["user-session-state:17", "user-session-state:18", "user-session-active:17:device-a",
            "user-session-active:17:device-b", "user-restaurant-access:17:5", "user-restaurant-access:17:6"];
        foreach (var key in keys) await cache.SetStringAsync(key, "legacy");
        var sessions = Sessions(cache, Mock.Of<IUserRepository>(), Mock.Of<IUserRefreshTokenRepository>());
        await sessions.InvalidateAsync(17);
        await sessions.InvalidateSessionAsync(17, "device-a");
        await Membership(cache, Mock.Of<IUserRestaurantRepository>()).InvalidateAsync(17, 5);
        foreach (var key in keys.Where((_, index) => index % 2 == 0)) Assert.Null(await cache.GetStringAsync(key));
        foreach (var key in keys.Where((_, index) => index % 2 == 1)) Assert.Equal("legacy", await cache.GetStringAsync(key));
    }

    /// <summary>Yalnız test yaddaşında köhnə Redis məlumatını modelləşdirir.</summary>
    private static MemoryDistributedCache Cache() => new(Options.Create(new MemoryDistributedCacheOptions()));

    /// <summary>İstehsal sessiya oxuyucusunu yalnız verilən test repository-ləri ilə qurur.</summary>
    private static UserSessionStateCache Sessions(IDistributedCache cache, IUserRepository users, IUserRefreshTokenRepository tokens)
        => new(cache, users, tokens, new ConfigurationBuilder().Build(), NullLogger<UserSessionStateCache>.Instance);

    /// <summary>Restoran sərhədi oxuyucusunu canlı xidmətlərə qoşulmadan qurur.</summary>
    private static UserRestaurantAccessCache Membership(IDistributedCache cache, IUserRestaurantRepository repository)
        => new(cache, repository, new ConfigurationBuilder().Build(), NullLogger<UserRestaurantAccessCache>.Instance);

    /// <summary>Köhnə access tokeni olan test sorğusu yaradır; həqiqi token və şəxsi məlumat işlətmir.</summary>
    private static DefaultHttpContext Request()
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new("userId", "17"), new("sessionVersion", "7"), new("sessionId", "device-a")], "test")) };
        context.Request.Path = "/api/v1/profile";
        return context;
    }
}
