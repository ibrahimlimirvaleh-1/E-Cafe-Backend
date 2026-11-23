using System.Security.Claims;
using System.Reflection;
using AutoMapper;
using ECafe.Api.Controllers;
using ECafe.Api.Security;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Repository;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Auth.Concrete;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.User;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using StoredUser = ECafe.Domain.Entities.User;

namespace ECafe.Tests;

public sealed class CustomerAccountDeletionTests
{
    [Fact]
    public void EndpointHasCustomerRoleAndUserPartitionedMutationLimit()
    {
        Assert.Equal("5", Assert.Single(typeof(CustomerAccountDeletionController).GetCustomAttributes<AuthorizeAttribute>()).Roles);
        Assert.Equal(RateLimitPolicyNames.CustomerAccountDeletion,
            typeof(CustomerAccountDeletionController).GetMethod(nameof(CustomerAccountDeletionController.Delete))!
                .GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal(RateLimitPolicyNames.PublicRead,
            typeof(CustomerAccountDeletionController).GetMethod(nameof(CustomerAccountDeletionController.GetEligibility))!
                .GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.DoesNotContain(typeof(CustomerAccountDeletionRequest).GetProperties(), value => value.Name.Contains("UserId"));
    }

    [Theory]
    [InlineData(RoleCode.Owner)] [InlineData(RoleCode.Manager)] [InlineData(RoleCode.Waiter)]
    [InlineData(RoleCode.Kitchen)] [InlineData(RoleCode.SuperAdmin)]
    public async Task StoredStaffOrAdminRoleCannotUseAnOldCustomerClaim(RoleCode role)
    {
        await using var f = await Fixture.CreateAsync();
        f.Account.RoleId = (int)role;
        await f.Db.SaveChangesAsync();
        Assert.Equal(ErrorCode.AccountDeletionCustomerOnly,
            (await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.DeleteAsync(Request(), default))).Code);
        Assert.True(f.Account.IsActive);
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("version")]
    [InlineData("device")]
    [InlineData("account")]
    [InlineData("principal")]
    public async Task RevokedOrMissingSessionCannotDeleteAccount(string state)
    {
        await using var f = await Fixture.CreateAsync();
        if (state == "version") f.SetPrincipal(version: 99);
        if (state == "device") f.Refresh.Setup(value => value.HasActiveUserSessionAsync(5, "current-device", It.IsAny<DateTime>())).ReturnsAsync(false);
        if (state == "account") f.Account.IsActive = false;
        if (state == "principal") f.Http.User = new ClaimsPrincipal(new ClaimsIdentity());
        await f.Db.SaveChangesAsync();
        Assert.Equal(ErrorCode.SessionInvalid,
            (await Assert.ThrowsAsync<UnauthorizedException>(() => f.Service.DeleteAsync(Request(), default))).Code);
        Assert.Equal("customer@example.test", f.Account.Email);
        Assert.Null(f.Tokens[0].RevokedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong-password")]
    public async Task InvalidPasswordOrMissingConfirmationNeverClosesAccount(string password)
    {
        await using var f = await Fixture.CreateAsync();
        if (password == string.Empty)
            await Assert.ThrowsAsync<ValidationException>(() => f.Service.DeleteAsync(new() { CurrentPassword = "correct-password", Confirm = false }, default));
        else
        {
            Assert.Equal(ErrorCode.InvalidCredentials,
                (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync(new() { CurrentPassword = password, Confirm = true }, default))).Code);
            f.Login.Verify(value => value.RecordFailureAsync(f.Account, "customer@example.test", "CustomerAccountDeletionReauthentication"), Times.Once);
            f.Transaction.Verify(value => value.CommitAsync(default), Times.Once);
        }
        Assert.True(f.Account.IsActive);
        Assert.Equal(1, f.Account.SessionVersion);
        Assert.All(f.Tokens, token => Assert.Null(token.RevokedAt));
    }

    [Theory]
    [InlineData(CustomerAccountClosureBlocker.ActiveStaffProfile)]
    [InlineData(CustomerAccountClosureBlocker.ActiveReservation)]
    [InlineData(CustomerAccountClosureBlocker.OpenTableSession)]
    [InlineData(CustomerAccountClosureBlocker.PendingPayment)]
    [InlineData(CustomerAccountClosureBlocker.PendingRefund)]
    [InlineData(CustomerAccountClosureBlocker.UnpaidBill)]
    [InlineData(CustomerAccountClosureBlocker.WalletBalance)]
    [InlineData(CustomerAccountClosureBlocker.UnresolvedOrder)]
    public async Task EveryOutstandingActivityIsRecheckedAtDeletion(CustomerAccountClosureBlocker blocker)
    {
        await using var f = await Fixture.CreateAsync();
        f.Closure.Setup(value => value.GetBlockersAsync(5, default)).ReturnsAsync(new[] { blocker });
        var eligibility = await f.Service.GetEligibilityAsync(default);
        Assert.False(eligibility.CanDelete);
        Assert.Equal(blocker.ToString(), Assert.Single(eligibility.Blockers));
        Assert.Equal(ErrorCode.AccountDeletionBlocked,
            (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync(Request(), default))).Code);
        Assert.True(f.Account.IsActive);
        f.Closure.Verify(value => value.RevokeRecoveryAndPushAsync(It.IsAny<int>(), It.IsAny<DateTime>(), default), Times.Never);
    }

    [Fact]
    public async Task PreflightDoesNotAuthorizeALaterDeletionWhenActivityAppears()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.True((await f.Service.GetEligibilityAsync(default)).CanDelete);
        f.Closure.Setup(value => value.GetBlockersAsync(5, default)).ReturnsAsync(new[] { CustomerAccountClosureBlocker.ActiveReservation });
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync(Request(), default));
        Assert.True(f.Account.IsActive);
    }

    [Fact]
    public async Task SuccessfulClosureAnonymizesProfileAndRevokesDevicesWithoutSoftDeletingHistory()
    {
        await using var f = await Fixture.CreateAsync();
        f.Account.File = new() { Id = 10, Name = "avatar", Url = "synthetic-avatar.png", Token = "synthetic-avatar", Extension = ".png", FileTypeId = (int)FileTypeCode.UserProfileImage };
        f.Db.Add(f.Account.File);
        await f.Db.SaveChangesAsync();
        var avatar = f.Account.File;
        await f.Service.DeleteAsync(Request(), default);
        Assert.False(f.Account.IsActive);
        Assert.False(f.Account.IsDeleted);
        Assert.Equal(2, f.Account.SessionVersion);
        Assert.EndsWith("@account.invalid", f.Account.Email);
        Assert.StartsWith("closed-", f.Account.Phone);
        Assert.Empty(f.Account.Password);
        Assert.Null(f.Account.FileId);
        Assert.Null(f.Account.File);
        Assert.Equal((int)FileTypeCode.TemporaryUpload, avatar!.FileTypeId);
        Assert.All(f.Tokens, token => Assert.NotNull(token.RevokedAt));
        Assert.NotNull(await f.Db.Users.SingleOrDefaultAsync(value => value.Id == 5));
        var audit = Assert.Single(await f.Db.AuditLogs.ToListAsync());
        Assert.Equal(5, audit.UserId);
        Assert.Equal("CustomerAccountDeleted", audit.Action);
        Assert.DoesNotContain("correct-password", audit.Action);
        f.Sessions.Verify(value => value.InvalidateAsync(5), Times.Once);
        f.Realtime.Verify(value => value.NotifyUserDeactivatedAsync(5, It.IsAny<string>(), default), Times.Once);
        await Assert.ThrowsAsync<UnauthorizedException>(() => f.Service.DeleteAsync(Request(), default));
    }

    [Fact]
    public async Task RealtimeFailureDoesNotTurnCommittedClosureIntoAnError()
    {
        await using var f = await Fixture.CreateAsync();
        f.Realtime.Setup(value => value.NotifyUserDeactivatedAsync(5, It.IsAny<string>(), default)).ThrowsAsync(new IOException("Synthetic delivery outage"));
        await f.Service.DeleteAsync(Request(), default);
        Assert.False(f.Account.IsActive);
        f.Transaction.Verify(value => value.CommitAsync(default), Times.Once);
    }

    /// <summary>Yalnız sintetik şifrəni saxlayan və test loguna çıxarmayan təsdiq sorğusu qurur.</summary>
    private static CustomerAccountDeletionRequest Request() => new() { CurrentPassword = "correct-password", Confirm = true };

    private sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Db { get; } = new(new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public StoredUser Account { get; } = new() { Id = 5, Name = "Test", Surname = "Customer", Email = "customer@example.test", Phone = "0501112233",
            RoleId = 5, IsActive = true, SessionVersion = 1, Password = BCrypt.Net.BCrypt.HashPassword("correct-password", 4) };
        public DefaultHttpContext Http { get; } = new();
        public List<UserRefreshToken> Tokens { get; } = [new() { UserId = 5 }, new() { UserId = 5 }];
        public Mock<IUserRefreshTokenRepository> Refresh { get; } = new();
        public Mock<ICustomerAccountClosureRepository> Closure { get; } = new();
        public Mock<ILoginAttemptService> Login { get; } = new();
        public Mock<IUserSessionStateCache> Sessions { get; } = new();
        public Mock<IUserRealtimeNotifier> Realtime { get; } = new();
        public Mock<IApplicationDbTransaction> Transaction { get; } = new();
        public CustomerAccountDeletionManager Service { get; }

        /// <summary>DB sətirini gerçək repository ilə, xarici kanalları və transaction sayğaclarını saxta xidmətlərlə qurur.</summary>
        private Fixture()
        {
            SetPrincipal();
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(value => value.BeginTransactionAsync(default)).ReturnsAsync(Transaction.Object);
            Refresh.Setup(value => value.HasActiveUserSessionAsync(5, "current-device", It.IsAny<DateTime>())).ReturnsAsync(true);
            Refresh.Setup(value => value.GetActiveByUserIdTrackedAsync(5, It.IsAny<DateTime>())).ReturnsAsync(Tokens);
            Closure.Setup(value => value.GetBlockersAsync(5, default)).ReturnsAsync(Array.Empty<CustomerAccountClosureBlocker>());
            Service = new(new UserRepository(Db), Refresh.Object, Closure.Object, transactions.Object, Login.Object, Sessions.Object,
                Realtime.Object, new BaseRepository<AuditLog>(Db), Mock.Of<IHttpContextAccessor>(value => value.HttpContext == Http), TimeProvider.System,
                new TranslationBootstrap(), NullLogger<CustomerAccountDeletionManager>.Instance);
        }

        /// <summary>Köhnə versiya sınaqları üçün yalnız təsdiqlənmiş test principal-ını dəyişir.</summary>
        public void SetPrincipal(int version = 1) => Http.User = new(new ClaimsIdentity([
            new("userId", "5"), new(ClaimTypes.Role, "5"), new("sessionVersion", version.ToString()), new("sessionId", "current-device")], "test"));

        /// <summary>Həqiqi istifadəçi rolunun token claim-indən asılı olmadığını sınamaq üçün aktiv müştəri sətri yaradır.</summary>
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            f.Db.Users.Add(f.Account);
            await f.Db.SaveChangesAsync();
            return f;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
