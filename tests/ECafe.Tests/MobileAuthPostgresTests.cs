using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repository;
using ECafe.Application.Services;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Auth.Concrete;
using ECafe.Application.Services.Jwt.Concrete;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.Monitoring.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRefreshToken;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace ECafe.Tests;

public sealed class MobileAuthPostgresTests
{
    private const string OriginalToken = "native-auth-test-original-not-a-live-secret";
    private const string OtherDeviceToken = "native-auth-test-other-device-not-a-live-secret";
    private const string Session = "native-test-device";

    [StockPostgresFact]
    public async Task LoginPreservesAllActiveRolesAndRefreshKeepsSessionAndSevenDayLifetime()
    {
        await using var fixture = await Database.CreateAsync();
        foreach (var role in Enum.GetValues<RoleCode>())
        {
            await using (var setup = fixture.Context())
            {
                (await setup.Users.SingleAsync(u => u.Id == 1001)).RoleId = (int)role;
                var assignment = await setup.UserRestaurants.SingleAsync();
                assignment.RoleId = (int)role;
                assignment.IsActive = role is not (RoleCode.Customer or RoleCode.SuperAdmin);
                await setup.SaveChangesAsync();
            }
            AuthResponseDto login;
            await using (var db = fixture.Context())
                login = await Service(db).LoginAsync(new() { Email = " TEST1001@example.test ", Password = "test-password" });
            var claims = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken).Claims.ToList();
            Assert.Contains(claims, claim => claim.Type == ClaimTypes.Role && claim.Value == ((int)role).ToString());
            var sessionId = Assert.Single(claims, claim => claim.Type == "sessionId").Value;
            Assert.False(string.IsNullOrWhiteSpace(sessionId));
            var before = DateTime.UtcNow;
            await using (var db = fixture.Context())
            {
                var refreshed = await Service(db).RefreshAsync(new() { RefreshToken = login.RefreshToken });
                Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
                var refreshedClaims = new JwtSecurityTokenHandler().ReadJwtToken(refreshed.AccessToken).Claims.ToList();
                Assert.Equal(sessionId, Assert.Single(refreshedClaims, claim => claim.Type == "sessionId").Value);
                Assert.Contains(refreshedClaims, claim => claim.Type == ClaimTypes.Role && claim.Value == ((int)role).ToString());
                if (role is RoleCode.Owner or RoleCode.Manager or RoleCode.Waiter or RoleCode.Kitchen)
                    Assert.Contains(refreshedClaims, claim => claim.Type == "restaurantRoles" && claim.Value == $"1001:{(int)role}");
            }
            await using var read = fixture.Context();
            var rows = await read.UserRefreshTokens.Where(t => t.SessionId == sessionId).ToListAsync();
            Assert.Equal(2, rows.Count);
            var active = Assert.Single(rows, t => t.RevokedAt == null);
            Assert.DoesNotContain(login.RefreshToken, rows.Select(t => t.TokenHash));
            Assert.InRange(active.ExpiresAt, before.AddDays(7).AddSeconds(-1), DateTime.UtcNow.AddDays(7).AddSeconds(1));
        }
    }

    [StockPostgresFact]
    public async Task LateLogoutWithOriginalRotatedTokenRevokesOnlyItsFamilyAndIsIdempotent()
    {
        await using var fixture = await Database.CreateAsync();
        await using (var db = fixture.Context())
            await Service(db).RefreshAsync(new() { RefreshToken = OriginalToken });

        var cache = new Mock<IUserSessionStateCache>();
        cache.Setup(c => c.InvalidateSessionAsync(1001, Session)).Returns(async () =>
        {
            await using var read = fixture.Context();
            Assert.False(await read.UserRefreshTokens.AnyAsync(t => t.SessionId == Session && t.RevokedAt == null));
        });
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var db = fixture.Context();
            await Service(db, cache.Object).LogoutSessionAsync(new() { RefreshToken = OriginalToken });
        }
        await AssertOnlyOtherDeviceActiveAsync(fixture);
        cache.Verify(c => c.InvalidateSessionAsync(1001, Session), Times.Exactly(2));
        cache.Verify(c => c.InvalidateAsync(It.IsAny<int>()), Times.Never);
        await using var unknown = fixture.Context();
        await Service(unknown).LogoutSessionAsync(new() { RefreshToken = "unknown" });
        await Service(unknown).LogoutSessionAsync(new() { RefreshToken = "" });
    }

    [StockPostgresFact]
    public async Task NativeLogoutCanFollowLegacyTokenIntoItsNewSessionWithoutRevokingOtherDevices()
    {
        await using var fixture = await Database.CreateAsync();
        await using (var setup = fixture.Context())
        {
            (await setup.UserRefreshTokens.SingleAsync(t => t.TokenHash == Hash(OriginalToken))).SessionId = "";
            await setup.SaveChangesAsync();
        }
        await using (var db = fixture.Context())
            await Service(db).RefreshAsync(new() { RefreshToken = OriginalToken });
        await using (var db = fixture.Context())
            await Service(db).LogoutSessionAsync(new() { RefreshToken = OriginalToken });
        await using var read = fixture.Context();
        var active = Assert.Single(await read.UserRefreshTokens.Where(t => t.RevokedAt == null).ToListAsync());
        Assert.Equal(Hash(OtherDeviceToken), active.TokenHash);
        Assert.Equal(0, (await read.Users.SingleAsync(u => u.Id == 1001)).SessionVersion);
    }

    [StockPostgresFact]
    public async Task ParallelRefreshRotatesOnceAndCommitsReuseRevocationWithoutAffectingOtherDevices()
    {
        await using var fixture = await Database.CreateAsync();
        // Ayrı DbContext-lər eyni tokeni həqiqi PostgreSQL kilidi altında yeniləyir.
        async Task<Exception?> Refresh()
        {
            await using var db = fixture.Context();
            return await AttemptAsync(async () => { await Service(db).RefreshAsync(new() { RefreshToken = OriginalToken }); });
        }
        var results = await Task.WhenAll(Task.Run(Refresh), Task.Run(Refresh));
        Assert.Single(results, error => error == null);
        Assert.Equal(ErrorCode.RefreshTokenReuseDetected, Assert.IsType<ForbiddenException>(Assert.Single(results, error => error != null)).Code);
        await AssertOnlyOtherDeviceActiveAsync(fixture);
        await using var read = fixture.Context();
        Assert.Equal(2, await read.UserRefreshTokens.CountAsync(t => t.SessionId == Session));
    }

    [StockPostgresFact]
    public async Task NativeLogoutAndRefreshCannotLeaveAnActiveFamilyInEitherRaceOrder()
    {
        foreach (var refreshFirst in new[] { true, false })
        {
            await using var fixture = await Database.CreateAsync();
            var barrier = new SaveBarrier();
            await using var firstDb = fixture.Context(barrier);
            await using var secondDb = fixture.Context();
            var firstService = Service(firstDb);
            var secondService = Service(secondDb);
            var first = Task.Run(() => AttemptAsync(async () =>
            {
                if (refreshFirst) await firstService.RefreshAsync(new() { RefreshToken = OriginalToken });
                else await firstService.LogoutSessionAsync(new() { RefreshToken = OriginalToken });
            }));
            Task<Exception?>? second = null;
            try
            {
                await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                second = Task.Run(() => AttemptAsync(async () =>
                {
                    if (refreshFirst) await secondService.LogoutSessionAsync(new() { RefreshToken = OriginalToken });
                    else await secondService.RefreshAsync(new() { RefreshToken = OriginalToken });
                }));
                await Task.Delay(100);
                Assert.False(second.IsCompleted);
            }
            finally { barrier.Release.TrySetResult(); }
            Assert.Null(await first.WaitAsync(TimeSpan.FromSeconds(15)));
            var secondError = await second!.WaitAsync(TimeSpan.FromSeconds(15));
            if (refreshFirst) Assert.Null(secondError);
            else Assert.Equal(ErrorCode.RefreshTokenInvalid, Assert.IsType<UnauthorizedException>(secondError).Code);
            await AssertOnlyOtherDeviceActiveAsync(fixture);
        }
    }

    [StockPostgresFact]
    public async Task ExpiredRevokedInactiveAndWrongPasswordAreRejectedWithoutMintingTokens()
    {
        await using var fixture = await Database.CreateAsync();
        await using (var db = fixture.Context())
        {
            var auth = Service(db);
            Assert.Equal(ErrorCode.InvalidCredentials, (await Assert.ThrowsAsync<UnauthorizedException>(
                () => auth.LoginAsync(new() { Email = "test1001@example.test", Password = "wrong" }))).Code);
            Assert.Equal(ErrorCode.RefreshTokenInvalid, (await Assert.ThrowsAsync<UnauthorizedException>(
                () => auth.RefreshAsync(new() { RefreshToken = "unknown" }))).Code);
        }
        foreach (var state in new[] { "expired", "revoked", "inactive" })
        {
            await using (var setup = fixture.Context())
            {
                var token = await setup.UserRefreshTokens.SingleAsync(t => t.TokenHash == Hash(OriginalToken));
                token.ExpiresAt = state == "expired" ? DateTime.UtcNow.AddSeconds(-1) : DateTime.UtcNow.AddDays(1);
                token.RevokedAt = state == "revoked" ? DateTime.UtcNow : null;
                (await setup.Users.SingleAsync(u => u.Id == 1001)).IsActive = state != "inactive";
                await setup.SaveChangesAsync();
            }
            await using var db = fixture.Context();
            var error = await AttemptAsync(async () => { await Service(db).RefreshAsync(new() { RefreshToken = OriginalToken }); });
            if (state == "inactive") Assert.IsType<ForbiddenException>(error);
            else Assert.Equal(ErrorCode.RefreshTokenInvalid, Assert.IsType<UnauthorizedException>(error).Code);
            Assert.Equal(2, await db.UserRefreshTokens.CountAsync());
        }
    }

    [StockPostgresFact]
    public async Task TransientRotationSaveFailureRollsBackAndOriginalTokenCanBeRetried()
    {
        await using var fixture = await Database.CreateAsync();
        await using (var failing = fixture.Context(new FailingSave()))
            await Assert.ThrowsAsync<DbUpdateException>(() => Service(failing).RefreshAsync(new() { RefreshToken = OriginalToken }));
        await using (var read = fixture.Context())
        {
            Assert.Null((await read.UserRefreshTokens.SingleAsync(t => t.TokenHash == Hash(OriginalToken))).RevokedAt);
            Assert.Equal(2, await read.UserRefreshTokens.CountAsync());
        }
        await using var retry = fixture.Context();
        var response = await Service(retry).RefreshAsync(new() { RefreshToken = OriginalToken });
        Assert.NotEmpty(response.RefreshToken);
    }

    /// <summary>Test istifadəçisinin digər cihazının aktiv qaldığını və çıxılan sessiyanın artıq yenilənmədiyini bazadan yoxlayır.</summary>
    private static async Task AssertOnlyOtherDeviceActiveAsync(Database fixture)
    {
        await using var read = fixture.Context();
        Assert.False(await read.UserRefreshTokens.AnyAsync(t => t.SessionId == Session && t.RevokedAt == null));
        Assert.Null((await read.UserRefreshTokens.SingleAsync(t => t.TokenHash == Hash(OtherDeviceToken))).RevokedAt);
        Assert.Equal(0, (await read.Users.SingleAsync(u => u.Id == 1001)).SessionVersion);
    }

    /// <summary>Gözlənilən auth xətasını paralel testdə müqayisə olunan nəticəyə çevirir; tokenləri çıxışa yazmır.</summary>
    private static async Task<Exception?> AttemptAsync(Func<Task> operation)
    {
        try { await operation(); return null; }
        catch (Exception error) { return error; }
    }

    /// <summary>İstehsaldakı AuthManager, mapper, token repository və transaction sərhədini saxlayır; yalnız xarici xidmətləri saxtalaşdırır.</summary>
    private static AuthManager Service(ECafeDbContext db, IUserSessionStateCache? cache = null)
    {
        var mapper = new MapperConfiguration(c => c.AddProfile<AuthProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "native-auth-tests-only-0123456789012345678901234567890123456789",
            ["Jwt:Issuer"] = "ecafe-native-auth-tests",
            ["Jwt:Audience"] = "ecafe-native-auth-tests",
            ["Jwt:AccessTokenLifetimeMinutes"] = "15"
        }).Build();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var jwt = new JwtManager(accessor, mapper, configuration);
        return new(accessor, mapper, configuration, new UserRepository(db), new UserRefreshTokenRepository(db), jwt,
            Mock.Of<IMinioService>(), Mock.Of<IFileRepository>(), Mock.Of<ILoginAttemptService>(),
            Mock.Of<IEmailOutboxService>(), Mock.Of<ICriticalEventReporter>(), new Transactions(db),
            cache ?? Mock.Of<IUserSessionStateCache>());
    }

    /// <summary>Saxta refresh tokeni istehsaldakı SHA-256 formatında saxlayır; açıq token DB-yə yazılmır.</summary>
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class Transactions(ECafeDbContext db) : IApplicationDbTransactionFactory
    {
        /// <summary>Auth əməliyyatları üçün real PostgreSQL transaction açır.</summary>
        public async Task<IApplicationDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => new Transaction(await db.Database.BeginTransactionAsync(cancellationToken));
        /// <summary>İzolyasiya səviyyəsi göstərilən çağırış üçün də real transaction istifadə edir.</summary>
        public async Task<IApplicationDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
            => new Transaction(await db.Database.BeginTransactionAsync(isolationLevel, cancellationToken));
    }

    private sealed class Transaction(IDbContextTransaction transaction) : IApplicationDbTransaction
    {
        /// <summary>Yazıları real bazada təsdiqləyir.</summary>
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        /// <summary>Uğursuz sınaq əməliyyatını geri alır.</summary>
        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);
        /// <summary>Commit olunmamış əməliyyatı dispose zamanı geri alan DB transaction-ı bağlayır.</summary>
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private sealed class SaveBarrier : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>Birinci auth sorğusunu kilid əlində olduğu yazı mərhələsində saxlayır ki, ikinci real bağlantının gözləməsi yoxlanılsın.</summary>
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }

    private sealed class FailingSave : SaveChangesInterceptor
    {
        /// <summary>Rotasiya yazısının müvəqqəti DB xətasını yaradıb rollback qorumasını test edir.</summary>
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => throw new DbUpdateException("Synthetic transient test failure.");
    }

    private sealed class Database : IAsyncDisposable
    {
        private readonly string name = "ecafe_native_auth_" + Guid.NewGuid().ToString("N");
        private readonly string admin;
        private readonly string connection;

        public Database()
        {
            var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ECAFE_STOCK_TEST_CONNECTION"));
            if (builder.Host is not ("127.0.0.1" or "localhost") || builder.Port == 5432 || builder.Database != "postgres")
                throw new InvalidOperationException("Use a dedicated loopback PostgreSQL test cluster, non-default port, postgres admin database.");
            builder.Pooling = false;
            admin = builder.ConnectionString;
            builder.Database = name;
            connection = builder.ConnectionString;
        }

        /// <summary>Hər sorğuya ayrıca real bağlantı və lazım olduqda yazı maneəsi verir.</summary>
        public ECafeDbContext Context(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<ECafeDbContext>().UseNpgsql(connection, o => o.CommandTimeout(15));
            if (interceptor != null) options.AddInterceptors(interceptor);
            return new(options.Options);
        }

        /// <summary>Yalnız yeni GUID-li test bazasında sintetik istifadəçi və iki cihaz sessiyası yaradır.</summary>
        public static async Task<Database> CreateAsync()
        {
            var fixture = new Database();
            await using var admin = new NpgsqlConnection(fixture.admin);
            await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {fixture.name}", admin).ExecuteNonQueryAsync();
            try
            {
                await using var db = fixture.Context();
                await db.Database.EnsureCreatedAsync();
                db.Restaurants.Add(new Restaurant { Id = 1001, Name = "Native auth test", Location = "Baku", Phone = "1001", IsActive = true });
                db.Users.Add(new User
                {
                    Id = 1001, Name = "Native", Surname = "Test", Email = "test1001@example.test", Phone = "1001",
                    Password = BCrypt.Net.BCrypt.HashPassword("test-password", 4), PasswordSetAt = DateTime.UtcNow,
                    IsActive = true, RoleId = (int)RoleCode.Customer
                });
                db.UserRestaurants.Add(new UserRestaurant { UserId = 1001, RestaurantId = 1001, RoleId = (int)RoleCode.Customer, IsActive = false });
                db.UserRefreshTokens.AddRange(
                    new UserRefreshToken { UserId = 1001, TokenHash = Hash(OriginalToken), SessionId = Session, ExpiresAt = DateTime.UtcNow.AddDays(1) },
                    new UserRefreshToken { UserId = 1001, TokenHash = Hash(OtherDeviceToken), SessionId = "other-device", ExpiresAt = DateTime.UtcNow.AddDays(1) });
                await db.SaveChangesAsync();
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        /// <summary>Yalnız bu fixture-in yaratdığı test bazasını silir; canlı və istifadəçi bazalarına toxunmur.</summary>
        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", connection).ExecuteNonQueryAsync();
        }
    }
}
