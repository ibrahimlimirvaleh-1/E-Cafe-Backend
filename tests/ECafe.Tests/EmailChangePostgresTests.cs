using System.Security.Claims;
using System.Text.Json;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Repository;
using ECafe.Application.Services;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Auth.Concrete;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Monitoring.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Migrations;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRefreshToken;
using ECafe.Infrastructure.Repositories.LoginAttempt;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed partial class MobileAuthPostgresTests
{
    [StockPostgresFact]
    public async Task EmailConfirmationIsSingleUseAcrossSixteenIndependentApiScopes()
    {
        await using var database = await Database.CreateAsync();
        string token;
        await using (var db = database.Context())
        {
            await EmailChanges(db).RequestAsync(new() { NewEmail = "new@example.test", CurrentPassword = "test-password" });
            token = await EmailTokenAsync(db);
            Assert.Equal("test1001@example.test", (await db.Users.SingleAsync(x => x.Id == 1001)).Email);
            Assert.NotEqual(token, (await db.EmailChangeRequests.SingleAsync()).TokenHash);
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(async _ =>
        {
            await using var db = database.Context();
            try { await EmailChanges(db).ConfirmAsync(new() { Token = token }); return true; }
            catch (BaseException e) when (e.Code is ErrorCode.SessionInvalid or ErrorCode.EmailChangeInvalidOrExpired) { return false; }
        }));
        Assert.Equal(1, results.Count(x => x));
        await using var read = database.Context();
        Assert.Equal("new@example.test", (await read.Users.SingleAsync(x => x.Id == 1001)).Email);
        Assert.Equal(1, (await read.Users.SingleAsync(x => x.Id == 1001)).SessionVersion);
        Assert.False(await read.UserRefreshTokens.AnyAsync(x => x.UserId == 1001 && x.RevokedAt == null));
        Assert.Equal(4, await read.OutboxEvents.CountAsync(x => x.EventType == OutboxEventTypes.EmailNotificationRequested));
        Assert.Equal(2, await read.AuditLogs.CountAsync(x => x.UserId == 1001));
    }

    [StockPostgresFact]
    public async Task EmailRequestLimitIsSharedAcrossParallelApiScopesWithoutRedis()
    {
        await using var database = await Database.CreateAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async i =>
        {
            await using var db = database.Context();
            try { await EmailChanges(db).RequestAsync(new() { NewEmail = $"new{i}@example.test", CurrentPassword = "test-password" }); return true; }
            catch (TooManyRequestsException) { return false; }
        }));
        Assert.Equal(3, results.Count(x => x));
        await using var read = database.Context();
        Assert.Equal(3, await read.EmailChangeRequests.CountAsync());
        Assert.Equal(1, await read.EmailChangeRequests.CountAsync(x => x.RevokedAt == null));
        Assert.Equal("test1001@example.test", (await read.Users.SingleAsync(x => x.Id == 1001)).Email);
    }

    [StockPostgresFact]
    public async Task EmailConfirmationFailureRollsBackEmailSessionsAuditAndTokenConsumption()
    {
        await using var database = await Database.CreateAsync();
        string token;
        await using (var db = database.Context())
        {
            await EmailChanges(db).RequestAsync(new() { NewEmail = "new@example.test", CurrentPassword = "test-password" });
            token = await EmailTokenAsync(db);
        }
        await using (var db = database.Context())
        {
            var failingEmail = new Mock<IEmailOutboxService>();
            failingEmail.Setup(x => x.EnqueueEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long?>()))
                .ThrowsAsync(new InvalidOperationException("Synthetic outbox failure"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => EmailChanges(db, failingEmail.Object).ConfirmAsync(new() { Token = token }));
        }
        await using var read = database.Context();
        Assert.Equal("test1001@example.test", (await read.Users.SingleAsync(x => x.Id == 1001)).Email);
        Assert.Equal(0, (await read.Users.SingleAsync(x => x.Id == 1001)).SessionVersion);
        Assert.Null((await read.EmailChangeRequests.SingleAsync()).UsedAt);
        Assert.Equal(2, await read.UserRefreshTokens.CountAsync(x => x.RevokedAt == null));
        Assert.Equal(1, await read.AuditLogs.CountAsync(x => x.UserId == 1001));
        Assert.Equal(2, await read.OutboxEvents.CountAsync(x => x.EventType == OutboxEventTypes.EmailNotificationRequested));
    }

    [StockPostgresFact]
    public async Task EmailRequestFailureAfterFirstOutboxSaveRollsBackAllRelatedWrites()
    {
        await using var database = await Database.CreateAsync();
        await using (var db = database.Context())
        {
            var mail = EmailOutbox(db);
            var calls = 0;
            var failingEmail = new Mock<IEmailOutboxService>();
            failingEmail.Setup(x => x.EnqueueEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long?>()))
                .Returns(async (string to, string name, string subject, string body, string aggregate, long id, string? entity, long? entityId) =>
                {
                    if (++calls == 2) throw new InvalidOperationException("Synthetic second email failure");
                    await mail.EnqueueEmailAsync(to, name, subject, body, aggregate, id, entity, entityId);
                });
            await Assert.ThrowsAsync<InvalidOperationException>(() => EmailChanges(db, failingEmail.Object).RequestAsync(new() { NewEmail = "new@example.test", CurrentPassword = "test-password" }));
        }
        await using var read = database.Context();
        Assert.Empty(await read.EmailChangeRequests.ToListAsync());
        Assert.Empty(await read.AuditLogs.Where(x => x.UserId == 1001).ToListAsync());
        Assert.Empty(await read.OutboxEvents.ToListAsync());
    }

    [StockPostgresFact]
    public async Task EmailReauthenticationFailuresPersistAndLockoutWorksAcrossScopes()
    {
        await using var database = await Database.CreateAsync();
        for (var i = 0; i < 5; i++)
        {
            await using var db = database.Context();
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => EmailChanges(db).RequestAsync(new()
                { NewEmail = "new@example.test", CurrentPassword = "wrong-password" }));
            Assert.Equal(ErrorCode.InvalidCredentials, error.Code);
        }
        await using (var db = database.Context())
        {
            var error = await Assert.ThrowsAsync<ForbiddenException>(() => EmailChanges(db).RequestAsync(new()
                { NewEmail = "new@example.test", CurrentPassword = "test-password" }));
            Assert.Equal(ErrorCode.AccountTemporarilyLocked, error.Code);
        }
        await using var read = database.Context();
        Assert.Equal(5, await read.LoginAttempts.CountAsync(x => x.UserId == 1001 && !x.IsSuccessful));
        Assert.Equal(0, await read.EmailChangeRequests.CountAsync());
    }

    [StockPostgresFact]
    public async Task TwoAccountsCannotClaimTheSameEmailEvenWhenBothPassTheAvailabilityRead()
    {
        await using var database = await Database.CreateAsync();
        var tokens = new Dictionary<int, string>();
        await using (var setup = database.Context())
        {
            setup.Users.Add(new() { Id = 1002, Name = "Second", Surname = "Test", Email = "second@example.test", Phone = "1002", Password = BCrypt.Net.BCrypt.HashPassword("test-password", 4), IsActive = true, RoleId = (int)RoleCode.Customer });
            setup.UserRefreshTokens.Add(new() { UserId = 1002, TokenHash = "second-session-hash", SessionId = Session, ExpiresAt = DateTime.UtcNow.AddDays(1) });
            await setup.SaveChangesAsync();
        }
        foreach (var id in new[] { 1001, 1002 })
        {
            await using var db = database.Context();
            await EmailChanges(db, userId: id).RequestAsync(new() { NewEmail = "shared@example.test", CurrentPassword = "test-password" });
            tokens[id] = await EmailTokenAsync(db, id);
        }
        var barrier = new EmailSaveBarrier();
        var results = await Task.WhenAll(new[] { 1001, 1002 }.Select(async id =>
        {
            await using var db = database.Context(barrier);
            try { await EmailChanges(db, userId: id).ConfirmAsync(new() { Token = tokens[id] }); return true; }
            catch (BusinessRuleException e) when (e.Code == ErrorCode.EmailChangeInvalidOrExpired) { return false; }
        }));
        Assert.Equal(1, results.Count(x => x));
        await using var read = database.Context();
        Assert.Equal(1, await read.Users.CountAsync(x => x.Email == "shared@example.test"));
        Assert.Equal(1, await read.EmailChangeRequests.CountAsync(x => x.UsedAt != null));
        var winnerId = await read.Users.Where(x => x.Email == "shared@example.test").Select(x => x.Id).SingleAsync();
        Assert.False(await read.UserRefreshTokens.AnyAsync(x => x.UserId == winnerId && x.RevokedAt == null));
        Assert.Equal(winnerId == 1001 ? 1 : 2, await read.UserRefreshTokens.CountAsync(x => x.RevokedAt == null));
    }

    [StockPostgresFact]
    public async Task EmailMigrationUpAndDownAffectOnlyNewTableAndHaveNoPendingModelChanges()
    {
        await using var database = await Database.CreateAsync();
        await using var db = database.Context();
        Assert.False(db.Database.HasPendingModelChanges());
        var migration = new AddVerifiedEmailChanges();
        var sql = db.GetService<IMigrationsSqlGenerator>();
        foreach (var command in sql.Generate(migration.DownOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        Assert.Equal(1, await db.Users.CountAsync(x => x.Id == 1001));
        foreach (var command in sql.Generate(migration.UpOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        Assert.Equal(0, await db.EmailChangeRequests.CountAsync());
        Assert.Equal(2, await db.UserRefreshTokens.CountAsync());
    }

    /// <summary>İstehsaldakı DB kilidini, transaction-ı və transactional outbox-u saxlayaraq yalnız cari test hesabını təsdiqləyir.</summary>
    private static EmailChangeManager EmailChanges(ECafeDbContext db, IEmailOutboxService? email = null, int userId = 1001)
    {
        var context = new DefaultHttpContext { User = new(new ClaimsIdentity([new("userId", userId.ToString()), new("sessionVersion", "0"), new("sessionId", Session)], "test")) };
        var accessor = Mock.Of<IHttpContextAccessor>(x => x.HttpContext == context);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "https://frontend.example.test" }).Build();
        return new(new UserRepository(db), new UserRefreshTokenRepository(db), new BaseRepository<EmailChangeRequest>(db),
            new BaseRepository<PasswordResetToken>(db), new BaseRepository<UserPasswordSetupToken>(db), new BaseRepository<AuditLog>(db),
            new Transactions(db), email ?? EmailOutbox(db), new LoginAttemptManager(new LoginAttemptRepository(db), EmailOutbox(db),
                Mock.Of<ICriticalEventReporter>(), accessor, configuration), Mock.Of<IUserSessionStateCache>(), accessor, configuration,
            TimeProvider.System, new TranslationBootstrap());
    }

    /// <summary>Məktub göndərmədən yalnız sintetik DB outbox yazılarını yaradır.</summary>
    private static IEmailOutboxService EmailOutbox(ECafeDbContext db)
        => new EmailOutboxManager(new BaseRepository<OutboxEvent>(db), Mock.Of<IEmailService>(), Mock.Of<ICriticalEventReporter>(), new ConfigurationBuilder().Build());

    /// <summary>Yalnız test bazasındakı məktubdan təsdiq tokenini yaddaşa götürür; token ekrana və loga çıxmır.</summary>
    private static async Task<string> EmailTokenAsync(ECafeDbContext db, int userId = 1001)
    {
        var mails = await db.OutboxEvents.Where(x => x.EventType == OutboxEventTypes.EmailNotificationRequested && x.AggregateId == userId).ToListAsync();
        var body = mails.Select(x => JsonSerializer.Deserialize<EmailNotificationOutboxPayload>(x.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Body)
            .Single(x => x.Contains("#token="));
        return body.Split("#token=")[1].Split('\n')[0].Trim();
    }

    private sealed class EmailSaveBarrier : SaveChangesInterceptor
    {
        private int arrived;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>İki hesabın eyni ünvanı boş gördükdən sonra paralel yazmasını deterministik qurur; unikal DB indeksini yoxlayır.</summary>
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrived) >= 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }
}
