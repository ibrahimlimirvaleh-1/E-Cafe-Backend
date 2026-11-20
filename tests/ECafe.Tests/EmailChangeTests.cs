using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ECafe.Api.Controllers;
using ECafe.Api.Security;
using ECafe.Application.Common.Audit;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Repository;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Services;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Auth.Concrete;
using ECafe.Application.Services.Localization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.User;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class EmailChangeTests
{
    [Fact]
    public async Task RequestKeepsOriginalAddressAndStoresOnlyHashAndQueuesBothEmails()
    {
        await using var f = await Fixture.CreateAsync();
        await f.RequestAsync();
        Assert.Equal("old@example.test", f.User.Email);
        var request = await f.Db.EmailChangeRequests.SingleAsync();
        Assert.Equal("new@example.test", request.NewEmail);
        Assert.Equal(Hash(f.Token), request.TokenHash);
        Assert.NotEqual(f.Token, request.TokenHash);
        Assert.Equal(2, f.Mail.Count);
        Assert.Contains(f.Mail, x => x.To == "new@example.test" && x.Body.Contains("#token="));
        Assert.Contains(f.Mail, x => x.To == "old@example.test" && !x.Body.Contains(f.Token));
        var audit = await f.Db.AuditLogs.SingleAsync();
        Assert.Equal(AuditActions.EmailChangeRequested, audit.Action);
        Assert.Null(audit.Metadata);
        Assert.Null(audit.NewValues);
    }

    [Fact]
    public async Task WrongPasswordRecordsFailureWithoutCreatingPendingChange()
    {
        await using var f = await Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.RequestAsync(new() { NewEmail = "new@example.test", CurrentPassword = "wrong" }));
        Assert.Equal(ErrorCode.InvalidCredentials, error.Code);
        Assert.Empty(f.Mail);
        Assert.Empty(f.Db.EmailChangeRequests);
        f.Login.Verify(x => x.RecordFailureAsync(f.User, f.User.Email, "EmailChangeReauthentication"), Times.Once);
        f.Transaction.Verify(x => x.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task ConfirmChangesEmailRevokesAllSessionsRecoveryLinksAndAuditsWithoutToken()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.PasswordResetTokens.Add(new() { UserId = 1001, TokenHash = "old-reset", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        f.Db.UserPasswordSetupTokens.Add(new() { UserId = 1001, TokenHash = "old-setup", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await f.Db.SaveChangesAsync();
        await f.RequestAsync();
        await f.Service.ConfirmAsync(new() { Token = f.Token });
        Assert.Equal("new@example.test", f.User.Email);
        Assert.Equal(1, f.User.SessionVersion);
        Assert.All(f.Db.UserRefreshTokens, x => Assert.NotNull(x.RevokedAt));
        Assert.NotNull((await f.Db.PasswordResetTokens.SingleAsync()).RevokedAt);
        Assert.NotNull((await f.Db.UserPasswordSetupTokens.SingleAsync()).UsedAt);
        Assert.NotNull((await f.Db.EmailChangeRequests.SingleAsync()).UsedAt);
        Assert.Equal(4, f.Mail.Count);
        Assert.Equal(AuditActions.EmailChangeConfirmed, f.Db.AuditLogs.OrderBy(x => x.Id).Last().Action);
        f.Sessions.Verify(x => x.InvalidateAsync(1001), Times.Once);
        await Assert.ThrowsAsync<UnauthorizedException>(() => f.Service.ConfirmAsync(new() { Token = f.Token }));
    }

    [Fact]
    public async Task NewRequestRevokesPreviousTokenAndOldTokenCannotBeUsed()
    {
        await using var f = await Fixture.CreateAsync();
        await f.RequestAsync();
        var oldToken = f.Token;
        await f.RequestAsync("second@example.test");
        Assert.NotEqual(oldToken, f.Token);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ConfirmAsync(new() { Token = oldToken }));
        Assert.Equal(ErrorCode.EmailChangeInvalidOrExpired, error.Code);
        await f.Service.ConfirmAsync(new() { Token = f.Token });
        Assert.Equal("second@example.test", f.User.Email);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("version")]
    [InlineData("email")]
    [InlineData("revoked")]
    public async Task ChangedSecurityStateInvalidatesPendingConfirmation(string mutation)
    {
        await using var f = await Fixture.CreateAsync();
        await f.RequestAsync();
        var token = f.Token;
        var request = await f.Db.EmailChangeRequests.SingleAsync();
        if (mutation == "expired") request.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        if (mutation == "version") { f.User.SessionVersion++; f.Actor(1001, 1); }
        if (mutation == "email") f.User.Email = "changed@example.test";
        if (mutation == "revoked") request.RevokedAt = DateTime.UtcNow;
        await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ConfirmAsync(new() { Token = token }));
        Assert.Equal(ErrorCode.EmailChangeInvalidOrExpired, error.Code);
        Assert.Null(request.UsedAt);
    }

    [Fact]
    public async Task AnotherAccountCannotUseTheTokenEvenWithItsOwnValidSession()
    {
        await using var f = await Fixture.CreateAsync();
        await f.RequestAsync();
        f.Db.Users.Add(new() { Id = 1002, Name = "Other", Surname = "Account", Email = "other@example.test", Phone = "1002", Password = "unused", IsActive = true, RoleId = (int)RoleCode.Customer });
        await f.Db.SaveChangesAsync();
        f.Actor(1002);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ConfirmAsync(new() { Token = f.Token }));
        Assert.Equal(ErrorCode.EmailChangeInvalidOrExpired, error.Code);
        Assert.Equal("old@example.test", f.User.Email);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InactiveOrRevokedSessionCannotRequest(bool inactive)
    {
        await using var f = await Fixture.CreateAsync();
        if (inactive) f.User.IsActive = false;
        else f.Refresh.Setup(x => x.HasActiveUserSessionAsync(1001, "email-session", It.IsAny<DateTime>())).ReturnsAsync(false);
        await Assert.ThrowsAsync<UnauthorizedException>(() => f.RequestAsync());
        Assert.Empty(f.Mail);
    }

    [Fact]
    public async Task ExistingAddressUsesGenericResponseAndStillCountsTowardsPersistentLimit()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.Users.Add(new() { Id = 1002, Name = "Other", Surname = "Account", Email = "taken@example.test", Phone = "1002", Password = "unused", IsActive = true, RoleId = (int)RoleCode.Customer });
        await f.Db.SaveChangesAsync();
        for (var i = 0; i < 3; i++) await f.RequestAsync("taken@example.test");
        Assert.Empty(f.Mail);
        Assert.Equal(3, await f.Db.EmailChangeRequests.CountAsync());
        var error = await Assert.ThrowsAsync<TooManyRequestsException>(() => f.RequestAsync());
        Assert.Equal(429, error.StatusCode);
        Assert.Equal(ErrorCode.TooManyRequests, error.Code);
        Assert.Equal("old@example.test", f.User.Email);
    }

    [Fact]
    public async Task AddressTakenAfterRequestCannotBeConfirmed()
    {
        await using var f = await Fixture.CreateAsync();
        await f.RequestAsync();
        f.Db.Users.Add(new() { Id = 1002, Name = "Other", Surname = "Account", Email = "new@example.test", Phone = "1002", Password = "unused", IsActive = true, RoleId = (int)RoleCode.Customer });
        await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ConfirmAsync(new() { Token = f.Token }));
        Assert.Equal(ErrorCode.EmailChangeInvalidOrExpired, error.Code);
        Assert.Equal("old@example.test", f.User.Email);
        Assert.All(f.Db.UserRefreshTokens, x => Assert.Null(x.RevokedAt));
    }

    [Theory]
    [InlineData("http://attacker.example")]
    [InlineData("https://example.test/?redirect=evil")]
    [InlineData("https://user:password@example.test")]
    [InlineData("https://example.test/#fragment")]
    public async Task ConfirmationLinkRejectsUntrustedBaseUrl(string baseUrl)
    {
        await using var f = await Fixture.CreateAsync(baseUrl);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.RequestAsync());
        Assert.Empty(f.Mail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("<script>bad</script>")]
    public async Task InvalidTokenIsRejectedBeforeOpeningTransaction(string token)
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.ConfirmAsync(new() { Token = token }));
        Assert.Empty(f.Mail);
        f.Transaction.Verify(x => x.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task EndpointContractRequiresAuthorizationRateLimitsAndDeletesCorrectCookiePath()
    {
        Assert.NotNull(typeof(EmailChangeController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal(RateLimitPolicyNames.EmailChangeRequest, typeof(EmailChangeController).GetMethod("RequestChange")!.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal(RateLimitPolicyNames.EmailChangeConfirm, typeof(EmailChangeController).GetMethod("Confirm")!.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        var service = new Mock<IEmailChangeService>();
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        var controller = new EmailChangeController(service.Object) { ControllerContext = new() { HttpContext = http } };
        Assert.IsType<AcceptedResult>(await controller.RequestChange(new(), default));
        Assert.IsType<NoContentResult>(await controller.Confirm(new(), default));
        var cookie = http.Response.Headers.SetCookie.ToString();
        Assert.Contains("path=" + ECafe.Application.Routes.Routes.Auth.CookiePath, cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("a@example.test\r\nBcc: another")]
    [InlineData("Name <a@example.test>")]
    [InlineData("a@example.test,b@example.test")]
    public async Task EmailHeaderInjectionAndMultipleRecipientsAreRejected(string address)
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => f.RequestAsync(address));
        Assert.Empty(f.Mail);
    }

    [Fact]
    public void EmailChangeMessagesHaveAllThreeLanguagesAndConsistentPlaceholders()
    {
        var entries = new TranslationBootstrap().Snapshot.Entries;
        var ui = entries.Where(x => x.Key.StartsWith("ui.emailChange.")).ToArray();
        Assert.Equal(26, ui.Length);
        Assert.All(ui, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Value.Az));
            Assert.False(string.IsNullOrWhiteSpace(entry.Value.En));
            Assert.False(string.IsNullOrWhiteSpace(entry.Value.Ru));
        });
        foreach (var language in new[] { "az", "en", "ru" })
            Assert.Contains("{{seconds}}", new TranslationBootstrap().Snapshot.ForLanguage(language).Messages["ui.emailChange.retryIn"]);
    }

    /// <summary>Saxta təsdiq tokeninin istehsaldakı hash formatını yoxlayır.</summary>
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Db { get; } = new(new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public User User { get; } = new() { Id = 1001, Name = "Email", Surname = "Test", Email = "old@example.test", Phone = "1001", Password = BCrypt.Net.BCrypt.HashPassword("test-password", 4), IsActive = true, RoleId = (int)RoleCode.Customer };
        public DefaultHttpContext Http { get; } = new();
        public Mock<IUserRefreshTokenRepository> Refresh { get; } = new();
        public Mock<ILoginAttemptService> Login { get; } = new();
        public Mock<IUserSessionStateCache> Sessions { get; } = new();
        public Mock<IApplicationDbTransaction> Transaction { get; } = new();
        public List<(string To, string Body)> Mail { get; } = [];
        public EmailChangeManager Service { get; }
        public string Token => Mail.Last(x => x.Body.Contains("#token=")).Body.Split("#token=")[1].Split('\n')[0].Trim();

        public Fixture(string baseUrl)
        {
            Actor(1001);
            Refresh.Setup(x => x.HasActiveUserSessionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync(true);
            Refresh.Setup(x => x.GetActiveByUserIdTrackedAsync(It.IsAny<int>(), It.IsAny<DateTime>()))
                .Returns((int id, DateTime now) => Db.UserRefreshTokens.Where(x => x.UserId == id && x.RevokedAt == null).ToListAsync());
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Transaction.Object);
            var emails = new Mock<IEmailOutboxService>();
            emails.Setup(x => x.EnqueueEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long?>()))
                .Callback((string to, string name, string subject, string body, string aggregate, long id, string? entity, long? entityId) => Mail.Add((to, body))).Returns(Task.CompletedTask);
            Service = new(new UserRepository(Db), Refresh.Object, new BaseRepository<EmailChangeRequest>(Db),
                new BaseRepository<PasswordResetToken>(Db), new BaseRepository<UserPasswordSetupToken>(Db), new BaseRepository<AuditLog>(Db),
                transactions.Object, emails.Object, Login.Object, Sessions.Object, Mock.Of<IHttpContextAccessor>(x => x.HttpContext == Http),
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = baseUrl }).Build(),
                TimeProvider.System, new TranslationBootstrap());
        }

        /// <summary>Test sorğusunun hesab və sessiya versiyasını yalnız principal-da dəyişir.</summary>
        public void Actor(int id, int version = 0) => Http.User = new(new ClaimsIdentity([new("userId", id.ToString()), new("sessionVersion", version.ToString()), new("sessionId", "email-session")], "test"));

        /// <summary>Normal istifadəçinin iki aktiv cihazı olan sintetik InMemory fixture yaradır.</summary>
        public static async Task<Fixture> CreateAsync(string baseUrl = "https://frontend.example.test")
        {
            var fixture = new Fixture(baseUrl);
            fixture.Db.Users.Add(fixture.User);
            fixture.Db.UserRefreshTokens.AddRange(new UserRefreshToken { UserId = 1001, TokenHash = "A", SessionId = "A", ExpiresAt = DateTime.UtcNow.AddDays(1) }, new UserRefreshToken { UserId = 1001, TokenHash = "B", SessionId = "B", ExpiresAt = DateTime.UtcNow.AddDays(1) });
            await fixture.Db.SaveChangesAsync();
            return fixture;
        }

        /// <summary>Yalnız fixture-in sintetik bazasını bağlayır.</summary>
        public ValueTask DisposeAsync() => Db.DisposeAsync();

        /// <summary>Parolla yeni ünvanın normallaşdırılmasını da əhatə edən sorğu yaradır.</summary>
        public Task RequestAsync(string email = " NEW@example.test ") => Service.RequestAsync(new() { NewEmail = email, CurrentPassword = "test-password" });
    }
}
