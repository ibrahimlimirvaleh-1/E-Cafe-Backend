using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Services;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Auth.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.PasswordResetToken;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserPasswordSetupToken;
using ECafe.Infrastructure.Repositories.UserRefreshToken;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed partial class MobileAuthPostgresTests
{
    private const string ResetToken = "batch-security-reset-test-only";
    private const string SetupToken = "batch-security-setup-test-only";
    private const string NewPassword = "batch-security-new-password";

    [StockPostgresFact]
    public async Task PasswordResetAndRefreshSerializeInBothOrdersWithoutSurvivingCredentials()
    {
        foreach (var refreshFirst in new[] { true, false })
        {
            await using var fixture = await Database.CreateAsync();
            await SeedSecurityLinksAsync(fixture);
            string? replacement = null;
            // Hər əməliyyat ayrıca bağlantıda işləyir; maneə birincinin user kilidini saxlayır.
            async Task Rotate(ECafeDbContext db) => replacement = (await Service(db).RefreshAsync(new() { RefreshToken = OriginalToken })).RefreshToken;
            var errors = await RunOrderedAsync(fixture,
                refreshFirst ? Rotate : ResetAsync,
                refreshFirst ? ResetAsync : Rotate);
            Assert.Null(errors.First);
            if (refreshFirst) Assert.Null(errors.Second);
            else Assert.IsType<UnauthorizedException>(errors.Second);
            await AssertResetStateAsync(fixture, replacement);
        }
    }

    [StockPostgresFact]
    public async Task SessionTerminationCannotMissAConcurrentReplacement()
    {
        foreach (var operation in new[] { "web-logout", "logout-all", "revoke-device" })
        {
            await using var fixture = await Database.CreateAsync();
            string? replacement = null;
            var errors = await RunOrderedAsync(fixture,
                async db => replacement = (await Service(db).RefreshAsync(new() { RefreshToken = OriginalToken })).RefreshToken,
                async db =>
                {
                    if (operation == "web-logout") await Service(db).LogoutAsync(new() { RefreshToken = OriginalToken });
                    else if (operation == "logout-all") await Service(db, httpContext: SecurityActor()).LogoutAllAsync();
                    else await new UserSessionManager(new HttpContextAccessor { HttpContext = SecurityActor() },
                        Mock.Of<IMapper>(), new ConfigurationBuilder().Build(), new UserRefreshTokenRepository(db),
                        Mock.Of<IUserSessionStateCache>(), new Transactions(db)).RevokeMySessionAsync(Session);
                });
            Assert.Null(errors.First);
            Assert.Null(errors.Second);
            await using var read = fixture.Context();
            Assert.False(await read.UserRefreshTokens.AnyAsync(t => t.SessionId == Session && t.RevokedAt == null));
            Assert.Equal(operation != "logout-all", await read.UserRefreshTokens.AnyAsync(t => t.SessionId == "other-device" && t.RevokedAt == null));
            await using var retry = fixture.Context();
            await Assert.ThrowsAsync<UnauthorizedException>(() => Service(retry).RefreshAsync(new() { RefreshToken = replacement! }));
        }
    }

    [StockPostgresFact]
    public async Task OldPasswordLoginCannotPassAConcurrentPasswordReset()
    {
        await using var fixture = await Database.CreateAsync();
        await SeedSecurityLinksAsync(fixture);
        var errors = await RunOrderedAsync(fixture, ResetAsync, async db =>
        {
            await Service(db).LoginAsync(new() { Email = "test1001@example.test", Password = "test-password" });
        });
        Assert.Null(errors.First);
        Assert.IsType<UnauthorizedException>(errors.Second);
        await AssertResetStateAsync(fixture, null);
        await using var login = fixture.Context();
        Assert.NotEmpty((await Service(login).LoginAsync(new() { Email = "test1001@example.test", Password = NewPassword })).AccessToken);
    }

    [StockPostgresFact]
    public async Task PasswordResetLinkIsConsumedOnceEvenWithParallelRequests()
    {
        await using var fixture = await Database.CreateAsync();
        await SeedSecurityLinksAsync(fixture);
        var errors = await RunOrderedAsync(fixture, ResetAsync,
            db => ResetService(db).ResetPasswordAsync(new() { Token = ResetToken, Password = "another-new-password" }));
        Assert.Null(errors.First);
        Assert.Equal(ErrorCode.PasswordResetTokenInvalidOrExpired, Assert.IsType<BusinessRuleException>(errors.Second).Code);
        await AssertResetStateAsync(fixture, null);
    }

    [StockPostgresFact]
    public async Task SetupLinkIsConsumedOnceAndCannotOverwriteAResetPassword()
    {
        await using var fixture = await Database.CreateAsync();
        await SeedSecurityLinksAsync(fixture, unsetPassword: true);
        var errors = await RunOrderedAsync(fixture,
            db => SetupService(db).SetPasswordAsync(new() { Token = SetupToken, Password = NewPassword }),
            db => SetupService(db).SetPasswordAsync(new() { Token = SetupToken, Password = "another-new-password" }));
        Assert.Null(errors.First);
        Assert.IsType<BusinessRuleException>(errors.Second);
        await using (var read = fixture.Context())
            Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, (await read.Users.SingleAsync(u => u.Id == 1001)).Password));

        await using var other = await Database.CreateAsync();
        await SeedSecurityLinksAsync(other, unsetPassword: true);
        await using (var db = other.Context()) await ResetAsync(db);
        await using (var db = other.Context())
            await Assert.ThrowsAsync<BusinessRuleException>(() => SetupService(db).SetPasswordAsync(new() { Token = SetupToken, Password = "overwrite-attempt" }));
        await AssertResetStateAsync(other, null);
    }

    /// <summary>Birinci sorğunu commit-dən əvvəl saxlayıb ikincinin eyni istifadəçi kilidini gözlədiyini yoxlayır.</summary>
    private static async Task<(Exception? First, Exception? Second)> RunOrderedAsync(Database fixture,
        Func<ECafeDbContext, Task> firstOperation, Func<ECafeDbContext, Task> secondOperation)
    {
        var barrier = new SaveBarrier();
        await using var firstDb = fixture.Context(barrier);
        await using var secondDb = fixture.Context();
        var first = Task.Run(() => AttemptAsync(() => firstOperation(firstDb)));
        Task<Exception?>? second = null;
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = Task.Run(() => AttemptAsync(() => secondOperation(secondDb)));
            await Task.Delay(100);
            Assert.False(second.IsCompleted);
        }
        finally { barrier.Release.TrySetResult(); }
        return (await first.WaitAsync(TimeSpan.FromSeconds(20)), await second!.WaitAsync(TimeSpan.FromSeconds(20)));
    }

    /// <summary>Sintetik hesabda yalnız test üçün hash-lənmiş bərpa və ilkin şifrə linkləri yaradır.</summary>
    private static async Task SeedSecurityLinksAsync(Database fixture, bool unsetPassword = false)
    {
        await using var db = fixture.Context();
        if (unsetPassword) (await db.Users.SingleAsync(u => u.Id == 1001)).PasswordSetAt = null;
        db.PasswordResetTokens.Add(new PasswordResetToken { UserId = 1001, TokenHash = Hash(ResetToken), ExpiresAt = DateTime.UtcNow.AddMinutes(30) });
        db.UserPasswordSetupTokens.Add(new UserPasswordSetupToken { UserId = 1001, TokenHash = Hash(SetupToken), ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await db.SaveChangesAsync();
    }

    /// <summary>Bərpadan sonra hər iki cihazın ləğvini və yeni parolun saxlanmasını yoxlayır.</summary>
    private static async Task AssertResetStateAsync(Database fixture, string? replacement)
    {
        await using var read = fixture.Context();
        Assert.False(await read.UserRefreshTokens.AnyAsync(t => t.RevokedAt == null));
        var user = await read.Users.SingleAsync(u => u.Id == 1001);
        Assert.Equal(1, user.SessionVersion);
        Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, user.Password));
        Assert.NotNull((await read.PasswordResetTokens.SingleAsync()).UsedAt);
        if (replacement != null)
        {
            await using var retry = fixture.Context();
            await Assert.ThrowsAsync<UnauthorizedException>(() => Service(retry).RefreshAsync(new() { RefreshToken = replacement }));
        }
    }

    /// <summary>Canlı məktub göndərmədən real repository və transaction-lı şifrə bərpa xidmətini qurur.</summary>
    private static PasswordResetManager ResetService(ECafeDbContext db) => new(new UserRepository(db),
        new PasswordResetTokenRepository(db), new UserRefreshTokenRepository(db), Mock.Of<IEmailOutboxService>(),
        new HttpContextAccessor { HttpContext = SecurityActor() }, new ConfigurationBuilder().Build(),
        NullLogger<PasswordResetManager>.Instance, Mock.Of<IUserSessionStateCache>(), new Transactions(db));

    /// <summary>Eyni hesab kilidi ilə şifrə bərpası əməliyyatını işlədir.</summary>
    private static Task ResetAsync(ECafeDbContext db) => ResetService(db).ResetPasswordAsync(new() { Token = ResetToken, Password = NewPassword });

    /// <summary>İlkin parol təyinini real bazada, xarici e-poçt xidmətini isə mock ilə qurur.</summary>
    private static PasswordSetupManager SetupService(ECafeDbContext db) => new(new UserRepository(db),
        new UserPasswordSetupTokenRepository(db), Mock.Of<IEmailOutboxService>(), new ConfigurationBuilder().Build(),
        new UserRefreshTokenRepository(db), new Transactions(db));

    /// <summary>Sessiyanı ləğv edən sintetik müştərinin autentifikasiya kontekstini yaradır.</summary>
    private static DefaultHttpContext SecurityActor() => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", "1001"),
            new Claim(ClaimTypes.Role, ((int)RoleCode.Customer).ToString()), new Claim("sessionId", Session) }, "test"))
    };
}
