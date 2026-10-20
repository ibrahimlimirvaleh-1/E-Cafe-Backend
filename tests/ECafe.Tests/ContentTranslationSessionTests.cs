using System.Data;
using System.Security.Claims;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.ContentLocalization.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.ContentLocalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ContentTranslationSessionTests
{
    private const string SessionId = "11111111111111111111111111111111";
    private const string OtherSessionId = "22222222222222222222222222222222";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-session")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    [InlineData(" 11111111111111111111111111111111 ")]
    public async Task MissingOrMalformedSessionClaimCannotReadOrWrite(string? sessionId)
    {
        await using var f = new Fixture();
        f.Context.User = Principal(sessionId);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetAsync("items", 7, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 1, Name = "Tea" }, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.DeleteAsync("items", 7, "en", 1, default));
        f.Content.VerifyNoOtherCalls();
        f.Transaction.VerifyNoOtherCalls();
        f.Audit.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("deleted")]
    [InlineData("other-user")]
    [InlineData("other-session")]
    public async Task InactiveOrUnownedSessionCannotUseAnotherActiveDevice(string state)
    {
        await using var f = new Fixture();
        switch (state)
        {
            case "missing": f.Db.Remove(f.Token); break;
            case "revoked": f.Token.RevokedAt = DateTime.UtcNow; break;
            case "expired": f.Token.ExpiresAt = DateTime.UtcNow.AddDays(-1); break;
            case "deleted": f.Token.IsDeleted = true; break;
            case "other-user":
                f.Token.User = NewUser(12);
                f.Db.Users.Add(f.Token.User);
                break;
            case "other-session": f.Token.SessionId = OtherSessionId; break;
        }
        await f.Db.SaveChangesAsync();
        Assert.True(await f.Access.HasActiveSessionAsync(11, OtherSessionId, default));
        Assert.False(await f.Access.HasActiveSessionAsync(11, SessionId, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetAsync("items", 7, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 1, Name = "Tea" }, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.DeleteAsync("items", 7, "en", 1, default));
        f.Content.VerifyNoOtherCalls();
        f.Audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RotatedSessionAcceptsActiveReplacementAndCanReadAndWrite()
    {
        await using var f = new Fixture();
        f.Token.RevokedAt = DateTime.UtcNow;
        f.Db.UserRefreshTokens.Add(new UserRefreshToken
        {
            UserId = 11, SessionId = SessionId, TokenHash = "replacement", ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        await f.Db.SaveChangesAsync();
        Assert.True(await f.Access.HasActiveSessionAsync(11, SessionId, default));
        await f.Service.GetAsync("items", 7, default);
        await f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 1, Name = "Tea" }, default);
        await f.Service.DeleteAsync("items", 7, "en", 2, default);
        f.Content.Verify(repository => repository.SaveAsync(ContentEntityType.Item, 7, 2, It.IsAny<ContentTranslationValue>(), 2, default), Times.Once);
        f.Content.Verify(repository => repository.DeleteAsync(ContentEntityType.Item, 7, 2, 2, default), Times.Once);
        f.Transaction.Verify(transaction => transaction.CommitAsync(default), Times.Exactly(2));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SessionRevocationOrExpiryWhileWaitingForLockDeniesWrite(bool delete, bool expire)
    {
        await using var f = new Fixture();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Content.Setup(repository => repository.AcquireLockAsync(ContentEntityType.Item, 7, default))
            .Returns(async () =>
            {
                waiting.SetResult();
                await release.Task;
            });
        Task write = delete
            ? f.Service.DeleteAsync("items", 7, "en", 1, default)
            : f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 1, Name = "Tea" }, default);
        try
        {
            await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (expire) f.Token.ExpiresAt = DateTime.UtcNow.AddDays(-1);
            else f.Token.RevokedAt = DateTime.UtcNow;
            await f.Db.SaveChangesAsync();
        }
        finally
        {
            release.TrySetResult();
        }
        await Assert.ThrowsAsync<ForbiddenException>(() => write);
        Assert.Equal(3, (await f.Access.GetActorAsync(11, default))!.SessionVersion);
        Assert.True(await f.Access.HasActiveSessionAsync(11, OtherSessionId, default));
        f.Content.Verify(repository => repository.GetSourceAsync(ContentEntityType.Item, 7, default), Times.Once);
        f.Content.Verify(repository => repository.SaveAsync(It.IsAny<ContentEntityType>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<ContentTranslationValue>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Content.Verify(repository => repository.DeleteAsync(It.IsAny<ContentEntityType>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.Audit.VerifyNoOtherCalls();
    }

    /// <summary>Sınaq üçün dəyişməyən hesab reviziyası və seçilmiş cihaz sessiyası ilə principal qurur.</summary>
    private static ClaimsPrincipal Principal(string? sessionId)
    {
        var claims = new List<Claim>
        {
            new("userId", "11"), new(ClaimTypes.Role, "3"), new("sessionVersion", "3"), new("restaurantRoles", "1:3")
        };
        if (sessionId != null) claims.Add(new("sessionId", sessionId));
        return new(new ClaimsIdentity(claims, "test"));
    }

    /// <summary>Sessiya aidiyyətinin yoxlanması üçün aktiv istifadəçi yaradır.</summary>
    private static User NewUser(int id) => new()
    {
        Id = id, RoleId = 3, SessionVersion = 3, IsActive = true,
        Name = "Test", Surname = "User", Email = $"test{id}@example.test", Phone = "000", Password = "test-only"
    };

    private sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Db { get; } = new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Mock<IContentTranslationRepository> Content { get; } = new();
        public Mock<IApplicationDbTransaction> Transaction { get; } = new();
        public Mock<IAuditLogService> Audit { get; } = new();
        public DefaultHttpContext Context { get; } = new() { User = Principal(SessionId) };
        public UserRefreshToken Token { get; }
        public ContentTranslationAccessRepository Access { get; }
        public ContentTranslationManager Service { get; }
        private long revision = 1;

        /// <summary>İki cihaz sessiyası olan canlı access repository-ni saxta məzmun və transaction sərhədlərinə bağlayır.</summary>
        public Fixture()
        {
            var user = NewUser(11);
            var role = new Role { Id = 3, Name = "Manager" };
            var restaurant = new Restaurant { Id = 1, Name = "Cafe", Location = "Baku", Phone = "000" };
            Token = new() { User = user, SessionId = SessionId, TokenHash = "original", ExpiresAt = DateTime.UtcNow.AddDays(1) };
            Db.AddRange(role, new UserRestaurant { User = user, Restaurant = restaurant, Role = role, IsActive = true }, Token,
                new UserRefreshToken { User = user, SessionId = OtherSessionId, TokenHash = "other-device", ExpiresAt = DateTime.UtcNow.AddDays(1) },
                new RolePermission { Role = role, Permission = new Permission { Id = (int)PermissionCode.ManageCatalog, Name = "ManageCatalog" } });
            Db.SaveChanges();
            Access = new(Db);
            Content.Setup(repository => repository.GetSourceAsync(ContentEntityType.Item, 7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentTranslationSource(7, 1, new("Cay")));
            Content.Setup(repository => repository.GetRowsAsync(ContentEntityType.Item, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new List<ContentTranslationRow> { new(7, "en", revision, new("Tea")) });
            Content.Setup(repository => repository.SaveAsync(ContentEntityType.Item, 7, 2, It.IsAny<ContentTranslationValue>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Callback((ContentEntityType _, int _, int _, ContentTranslationValue _, long nextRevision, CancellationToken _) => revision = nextRevision)
                .Returns(Task.CompletedTask);
            var languages = new Mock<IContentLanguageRepository>();
            languages.Setup(repository => repository.GetLanguagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ContentLanguage> { new(1, "az", true), new(2, "en", true) });
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(factory => factory.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Transaction.Object);
            Service = new(Content.Object, languages.Object,
                new ContentTranslationAccessManager(Content.Object, Access, new HttpContextAccessor { HttpContext = Context }),
                transactions.Object, Audit.Object);
        }

        /// <summary>Sınaq bazasının resurslarını digər sınaqlara təsir etmədən bağlayır.</summary>
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
