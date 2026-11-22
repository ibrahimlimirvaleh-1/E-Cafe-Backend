using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.User.Staff;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Services;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Application.Services.User.Concrete;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Role;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRefreshToken;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Shared.Services.Jwt.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed partial class MobileAuthPostgresTests
{
    [StockPostgresFact]
    public async Task ProfileDeactivationRejectsOldTenantClaimsButKeepsOtherTenantAndBothDeviceSessions()
    {
        foreach (var editForm in new[] { false, true })
        {
            await using var fixture = await ProfileDatabaseAsync();
            var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            Assert.True(await CanAuthorizeFromNewScopeAsync(fixture, cache));
            Assert.True(await CanAuthorizeFromNewScopeAsync(fixture, cache, restaurantId: 1002));
            await using (var db = fixture.Context())
                await DeactivateProfileAsync(db, editForm);

            // Hadisə çatmasa və ya köhnə token saxlanılsa belə tenant icazəsi bazadan rədd edilir.
            Assert.False(await CanAuthorizeFromNewScopeAsync(fixture, cache));
            Assert.True(await CanAuthorizeFromNewScopeAsync(fixture, cache, restaurantId: 1002));
            Assert.True(await CanAuthorizeFromNewScopeAsync(fixture, cache, restaurantId: 1002, sessionId: "other-device"));
            await using (var read = fixture.Context())
            {
                var user = await read.Users.SingleAsync(value => value.Id == 1001);
                Assert.True(user.IsActive);
                Assert.Equal(0, user.SessionVersion);
                Assert.Equal(2, await read.UserRefreshTokens.CountAsync(value => value.RevokedAt == null));
                var profile = await ProfileService(read).GetProfileAsync(1001);
                Assert.True(profile.IsActive);
                Assert.Equal(1002, Assert.Single(profile.Profiles).RestaurantId);
                Assert.False(await read.Users.Where(UserProfileAccessPolicy.PersonalAccounts).AnyAsync(value => value.Id == 1001));
            }
            foreach (var token in new[] { OriginalToken, OtherDeviceToken })
            {
                await using var db = fixture.Context();
                var refreshed = await Service(db).RefreshAsync(new() { RefreshToken = token });
                var claims = new JwtSecurityTokenHandler().ReadJwtToken(refreshed.AccessToken).Claims.ToList();
                Assert.Equal($"1002:{(int)RoleCode.Waiter}", Assert.Single(claims, value => value.Type == "restaurantRoles").Value);
            }
        }
    }

    [StockPostgresFact]
    public async Task LastProfileDeactivationRevokesBothDevicesAndRequiresFreshLoginAfterReactivation()
    {
        foreach (var editForm in new[] { false, true })
        {
            await using var fixture = await ProfileDatabaseAsync();
            await DisableSecondProfileAsync(fixture);
            await using (var db = fixture.Context()) await DeactivateProfileAsync(db, editForm);
            await AssertProfileSessionDeniedAsync(fixture, ErrorCode.StaffProfileInactive);
            await using (var db = fixture.Context())
            {
                Assert.Equal(ErrorCode.StaffProfileInactive,
                    (await Assert.ThrowsAsync<UnauthorizedException>(() => ProfileService(db).GetProfileAsync(1001))).Code);
                var user = await db.Users.SingleAsync(value => value.Id == 1001);
                Assert.True(user.IsActive);
                Assert.Equal((int)RoleCode.Waiter, user.RoleId);
                Assert.Equal(1, user.SessionVersion);
                Assert.False(await db.UserRefreshTokens.AnyAsync(value => value.RevokedAt == null));
                Assert.False(await db.Users.Where(UserProfileAccessPolicy.PersonalAccounts).AnyAsync(value => value.Id == 1001));
            }
            await using (var db = fixture.Context())
                Assert.Equal(ErrorCode.StaffProfileInactive, (await Assert.ThrowsAsync<UnauthorizedException>(() =>
                    Service(db).LoginAsync(new() { Email = "test1001@example.test", Password = "test-password" }))).Code);
            foreach (var token in new[] { OriginalToken, OtherDeviceToken })
            {
                await using var db = fixture.Context();
                await Assert.ThrowsAsync<UnauthorizedException>(() => Service(db).RefreshAsync(new() { RefreshToken = token }));
            }
            await using (var db = fixture.Context()) await ProfileService(db).ActivateStaffAsync(1001, 1001);
            await AssertProfileSessionDeniedAsync(fixture, ErrorCode.SessionInvalid);
            await using (var db = fixture.Context())
                await Assert.ThrowsAsync<UnauthorizedException>(() => Service(db).RefreshAsync(new() { RefreshToken = OriginalToken }));
            await using (var db = fixture.Context())
            {
                var response = await Service(db).LoginAsync(new() { Email = "test1001@example.test", Password = "test-password" });
                var claims = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken).Claims.ToList();
                Assert.Equal("4", Assert.Single(claims, value => value.Type == ClaimTypes.Role).Value);
                Assert.Equal("1001:4", Assert.Single(claims, value => value.Type == "restaurantRoles").Value);
            }
        }
    }

    [StockPostgresFact]
    public async Task StaffAlreadyInLegacyCustomerFallbackCannotUseLoginRefreshOrOldCustomerClaims()
    {
        await using var fixture = await ProfileDatabaseAsync();
        await using (var setup = fixture.Context())
        {
            foreach (var assignment in await setup.UserRestaurants.ToListAsync()) assignment.IsActive = false;
            await setup.SaveChangesAsync();
        }
        await AssertProfileSessionDeniedAsync(fixture, ErrorCode.StaffProfileInactive);
        await using (var db = fixture.Context())
        {
            Assert.Equal(ErrorCode.StaffProfileInactive, (await Assert.ThrowsAsync<UnauthorizedException>(() =>
                Service(db).RefreshAsync(new() { RefreshToken = OriginalToken }))).Code);
        }
        await using (var db = fixture.Context())
            Assert.Equal(ErrorCode.StaffProfileInactive, (await Assert.ThrowsAsync<UnauthorizedException>(() =>
                Service(db).LoginAsync(new() { Email = "test1001@example.test", Password = "test-password" }))).Code);
        await using (var db = fixture.Context())
            Assert.Equal(ErrorCode.InvalidCredentials, (await Assert.ThrowsAsync<UnauthorizedException>(() =>
                Service(db).LoginAsync(new() { Email = "test1001@example.test", Password = "wrong" }))).Code);
        await using (var db = fixture.Context()) await ProfileService(db).ActivateStaffAsync(1001, 1001);
        await using (var db = fixture.Context())
            await Assert.ThrowsAsync<UnauthorizedException>(() => Service(db).RefreshAsync(new() { RefreshToken = OriginalToken }));
    }

    [StockPostgresFact]
    public async Task OrdinaryCustomerWithoutStaffProfilesStillLogsInRefreshesAndKeepsItsAccount()
    {
        await using var fixture = await Database.CreateAsync();
        await using (var db = fixture.Context())
            AssertPersonalToken((await Service(db).LoginAsync(new() { Email = "test1001@example.test", Password = "test-password" })).AccessToken);
        await using (var db = fixture.Context())
            AssertPersonalToken((await Service(db).RefreshAsync(new() { RefreshToken = OriginalToken })).AccessToken);
        await using (var db = fixture.Context())
        {
            var profile = await ProfileService(db).GetProfileAsync(1001);
            Assert.True(profile.IsActive);
            Assert.Equal((int)RoleCode.Customer, profile.RoleId);
            Assert.Empty(profile.Profiles);
            Assert.True((await new UserRepository(db).GetSessionStateAsync(1001))!.Value.HasRequiredStaffProfile);
        }
    }

    [StockPostgresFact]
    public async Task LastStaffProfileAndRefreshSerializeInBothOrdersWithoutLeavingAnyUsableToken()
    {
        foreach (var refreshFirst in new[] { false, true })
        {
            await using var fixture = await ProfileDatabaseAsync();
            await DisableSecondProfileAsync(fixture);
            string? replacement = null;
            // İlk sorğu user kilidində saxlanılır ki, refresh və son profilin bağlanması hər iki sırada yoxlanılsın.
            async Task Rotate(ECafeDbContext db)
                => replacement = (await Service(db).RefreshAsync(new() { RefreshToken = OriginalToken })).RefreshToken;
            var errors = await RunOrderedAsync(fixture,
                refreshFirst ? Rotate : db => DeactivateProfileAsync(db, false),
                refreshFirst ? db => DeactivateProfileAsync(db, false) : Rotate);
            Assert.Null(errors.First);
            if (refreshFirst) Assert.Null(errors.Second);
            else Assert.IsType<UnauthorizedException>(errors.Second);
            await using var read = fixture.Context();
            Assert.False(await read.UserRefreshTokens.AnyAsync(value => value.RevokedAt == null));
            Assert.Equal(1, (await read.Users.SingleAsync(value => value.Id == 1001)).SessionVersion);
            if (replacement is not null)
                await Assert.ThrowsAsync<UnauthorizedException>(() => Service(read).RefreshAsync(new() { RefreshToken = replacement }));
        }
    }

    [StockPostgresFact]
    public async Task ParallelRemovalOfBothProfilesRevokesDevicesOnlyWhenTheLastProfileCommits()
    {
        await using var fixture = await ProfileDatabaseAsync();
        var errors = await RunOrderedAsync(fixture, db => ProfileService(db).DeactivateStaffAsync(1001, 1001),
            db => ProfileService(db).DeactivateStaffAsync(1002, 1001));
        Assert.Null(errors.First);
        Assert.Null(errors.Second);
        await using var read = fixture.Context();
        Assert.False(await read.UserRestaurants.AnyAsync(value => value.IsActive));
        Assert.False(await read.UserRefreshTokens.AnyAsync(value => value.RevokedAt == null));
        Assert.Equal(1, (await read.Users.SingleAsync(value => value.Id == 1001)).SessionVersion);
        Assert.True((await read.Users.SingleAsync(value => value.Id == 1001)).IsActive);
    }

    [StockPostgresFact]
    public async Task ProfileDeactivationAndRefreshSerializeInBothOrdersWithoutGlobalRevocation()
    {
        foreach (var refreshFirst in new[] { false, true })
        {
            await using var fixture = await ProfileDatabaseAsync();
            string? replacement = null;
            // Ayrı bağlantılar eyni user kilidini gözləyir; yalnız restoran profilinin statusu dəyişir.
            async Task Rotate(ECafeDbContext db)
                => replacement = (await Service(db).RefreshAsync(new() { RefreshToken = OriginalToken })).RefreshToken;
            var errors = await RunOrderedAsync(fixture,
                refreshFirst ? Rotate : db => DeactivateProfileAsync(db, false),
                refreshFirst ? db => DeactivateProfileAsync(db, false) : Rotate);
            Assert.Null(errors.First);
            Assert.Null(errors.Second);
            await using var verify = fixture.Context();
            var token = await Service(verify).RefreshAsync(new() { RefreshToken = replacement! });
            var claims = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken).Claims.ToList();
            Assert.Equal($"1002:{(int)RoleCode.Waiter}", Assert.Single(claims, value => value.Type == "restaurantRoles").Value);
            Assert.Equal(2, await verify.UserRefreshTokens.CountAsync(value => value.RevokedAt == null));
        }
    }

    [StockPostgresFact]
    public async Task FailedProfileUpdateRollsBackMembershipAndDoesNotEmitAnAccessEvent()
    {
        await using var fixture = await ProfileDatabaseAsync();
        var realtime = new Mock<IUserRealtimeNotifier>();
        await using (var db = fixture.Context(new FailingSave()))
            await Assert.ThrowsAsync<DbUpdateException>(() => ProfileService(db, realtime: realtime).DeactivateStaffAsync(1001, 1001));
        await using var read = fixture.Context();
        Assert.True((await read.UserRestaurants.SingleAsync(value => value.RestaurantId == 1001)).IsActive);
        Assert.Equal(2, await read.UserRefreshTokens.CountAsync(value => value.RevokedAt == null));
        Assert.Empty(realtime.Invocations);
    }

    [StockPostgresFact]
    public async Task ProfileActivationAndDeactivationSerializeAndReactivationIsVisibleOnRefresh()
    {
        foreach (var activateFirst in new[] { false, true })
        {
            await using var fixture = await ProfileDatabaseAsync();
            await using (var setup = fixture.Context())
            {
                (await setup.UserRestaurants.SingleAsync(value => value.RestaurantId == 1001)).IsActive = !activateFirst;
                await setup.SaveChangesAsync();
            }
            // Aktivləşdirmə və deaktivləşdirmə eyni user kilidi ilə ardıcıl commit olunmalıdır.
            Task Activate(ECafeDbContext db) => ProfileService(db).ActivateStaffAsync(1001, 1001);
            Task Deactivate(ECafeDbContext db) => DeactivateProfileAsync(db, false);
            var errors = await RunOrderedAsync(fixture, activateFirst ? Activate : Deactivate,
                activateFirst ? Deactivate : Activate);
            Assert.Null(errors.First);
            Assert.Null(errors.Second);
            await using var read = fixture.Context();
            Assert.Equal(!activateFirst, (await read.UserRestaurants.SingleAsync(value => value.RestaurantId == 1001)).IsActive);
            Assert.True((await read.Users.SingleAsync(value => value.Id == 1001)).IsActive);
            Assert.Equal(2, await read.UserRefreshTokens.CountAsync(value => value.RevokedAt == null));
            var token = await Service(read).RefreshAsync(new() { RefreshToken = OriginalToken });
            var roles = Assert.Single(new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken).Claims,
                value => value.Type == "restaurantRoles").Value;
            Assert.Equal(activateFirst ? "1002:4" : "1001:4,1002:4", roles);
        }
    }

    [StockPostgresFact]
    public async Task GlobalAccountBlockStillDeniesRefreshAndCannotBeClearedByProfileActivation()
    {
        await using var fixture = await ProfileDatabaseAsync();
        await using (var setup = fixture.Context())
        {
            (await setup.Users.SingleAsync(value => value.Id == 1001)).IsActive = false;
            (await setup.UserRestaurants.SingleAsync(value => value.RestaurantId == 1001)).IsActive = false;
            await setup.SaveChangesAsync();
        }
        await using (var db = fixture.Context())
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ProfileService(db).ActivateStaffAsync(1001, 1001));
            Assert.Equal(ErrorCode.StaffAccountInactive, error.Code);
        }
        await using (var db = fixture.Context())
            await Assert.ThrowsAsync<ForbiddenException>(() => Service(db).RefreshAsync(new() { RefreshToken = OriginalToken }));
    }

    /// <summary>Sınaq hesabında yalnız bir aktiv işçi profilini saxlayır; cihaz sessiyalarına toxunmur.</summary>
    private static async Task DisableSecondProfileAsync(Database fixture)
    {
        await using var db = fixture.Context();
        (await db.UserRestaurants.SingleAsync(value => value.RestaurantId == 1002)).IsActive = false;
        await db.SaveChangesAsync();
    }

    /// <summary>Köhnə müştəri fallback claim-i və köhnə versiya ilə yeni scope-da real middleware rəddini yoxlayır.</summary>
    private static async Task AssertProfileSessionDeniedAsync(Database fixture, ErrorCode expected)
    {
        await using var db = fixture.Context();
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var http = new DefaultHttpContext { User = new(new ClaimsIdentity([new("userId", "1001"),
            new("sessionVersion", "0"), new("sessionId", Session), new(ClaimTypes.Role, "5")], "test")) };
        http.Request.Path = "/api/v1/profile";
        var middleware = new ECafe.Api.Middlewares.ActiveUserMiddleware(_ => throw new InvalidOperationException("Must not reach endpoint"));
        Assert.Equal(expected, (await Assert.ThrowsAsync<UnauthorizedException>(() =>
            middleware.Invoke(http, AuthorizationSessions(db, cache)))).Code);
    }

    /// <summary>İki restoranlı sintetik icazə bazasını real personal hesabı roluna bağlayır.</summary>
    private static async Task<Database> ProfileDatabaseAsync()
    {
        var fixture = await AuthorizationDatabaseAsync();
        await using var db = fixture.Context();
        (await db.Users.SingleAsync(value => value.Id == 1001)).RoleId = (int)RoleCode.Waiter;
        foreach (var assignment in await db.UserRestaurants.ToListAsync())
            assignment.RoleId = (int)RoleCode.Waiter;
        db.RolePermissions.Add(new ECafe.Domain.Entities.RolePermission
            { RoleId = (int)RoleCode.Waiter, PermissionId = AuthorizationPermissionId });
        await db.SaveChangesAsync();
        return fixture;
    }

    /// <summary>Həm ayrıca deaktiv əmri, həm redaktə forması üçün eyni profil sərhədini yoxlayır.</summary>
    private static async Task DeactivateProfileAsync(ECafeDbContext db, bool editForm)
    {
        if (editForm)
            await ProfileService(db).UpdateStaffAsync(1001, 1001, new UpdateStaffRequest
            {
                Name = "Native", Surname = "Test", Email = "test1001@example.test", Phone = "0501234567", IsActive = false
            });
        else await ProfileService(db).DeactivateStaffAsync(1001, 1001);
    }

    /// <summary>Adi müştəri tokeninin şəxsi rolunu, restoran claim-lərinin yoxluğunu və müştəri icazəsini saxlayır.</summary>
    private static void AssertPersonalToken(string token)
    {
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToList();
        Assert.Equal(((int)RoleCode.Customer).ToString(), Assert.Single(claims, value => value.Type == ClaimTypes.Role).Value);
        Assert.DoesNotContain(claims, value => value.Type is "restaurantId" or "restaurantIds" or "restaurantRoles");
        Assert.All(claims.Where(value => value.Type == "permission"), value => Assert.Equal(nameof(PermissionCode.ViewRestaurantInfo), value.Value));
    }

    /// <summary>Real repository, transaction və row-lock ilə personal xidmətini qurur; yalnız xarici kanallar saxtadır.</summary>
    private static UserManager ProfileService(ECafeDbContext db, Mock<IUserRealtimeNotifier>? realtime = null)
    {
        var mapper = new MapperConfiguration(value =>
        {
            value.AddProfile<UserProfile>();
            value.AddProfile<AuthProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new("userId", "99"),
                new(ClaimTypes.Role, ((int)RoleCode.SuperAdmin).ToString())], "test"))
        };
        return new(new HttpContextAccessor { HttpContext = http }, mapper, new ConfigurationBuilder().Build(),
            Mock.Of<IRestaurantRepository>(), new RoleRepository(db), Mock.Of<IMinioService>(), Mock.Of<IFileRepository>(),
            new UserRepository(db), Mock.Of<IEmailOutboxService>(), Mock.Of<IJwtService>(), new UserRefreshTokenRepository(db),
            new UserRestaurantRepository(db), Mock.Of<IPasswordSetupService>(), Mock.Of<IAuditLogService>(),
            realtime?.Object ?? Mock.Of<IUserRealtimeNotifier>(), new Transactions(db), Mock.Of<IUserSessionStateCache>(),
            Mock.Of<IUserRestaurantAccessCache>());
    }
}
