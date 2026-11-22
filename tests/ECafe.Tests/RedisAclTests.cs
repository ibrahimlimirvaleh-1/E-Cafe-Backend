using System.Net;
using System.Net.Sockets;
using System.Text;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Infrastructure.Redis;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace ECafe.Tests;

public sealed class RedisAclFactAttribute : FactAttribute
{
    /// <summary>Canlı Redis-ə toxunmamaq üçün yalnız ayrıca ACL test mühiti verildikdə testi işlədir.</summary>
    public RedisAclFactAttribute()
    {
        if (new[] { "CACHE", "REALTIME", "VIEWER", "ADMIN" }.Any(role =>
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ECAFE_TEST_REDIS_ACL_" + role))))
            Skip = "Set the four ECAFE_TEST_REDIS_ACL_* variables to an isolated loopback Redis.";
    }
}

public sealed class RedisAclTests
{
    [RedisAclFact]
    public async Task AnonymousCommandsAndPasswordlessDefaultAuthenticationAreRejected()
    {
        var settings = Settings("ADMIN");
        var endpoint = settings.EndPoints.Single();
        var (host, port) = endpoint switch
        {
            DnsEndPoint dns => (dns.Host, dns.Port),
            IPEndPoint ip => (ip.Address.ToString(), ip.Port),
            _ => throw new InvalidOperationException("Unsupported test endpoint.")
        };
        using var socket = new TcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(host, port, timeout.Token);
        var stream = socket.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("*1\r\n$4\r\nPING\r\n"), timeout.Token);
        var buffer = new byte[512];
        var count = await stream.ReadAsync(buffer, timeout.Token);
        Assert.StartsWith("-NOAUTH", Encoding.ASCII.GetString(buffer, 0, count));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("*3\r\n$4\r\nAUTH\r\n$7\r\ndefault\r\n$0\r\n\r\n"), timeout.Token);
        count = await stream.ReadAsync(buffer, timeout.Token);
        Assert.StartsWith("-WRONGPASS", Encoding.ASCII.GetString(buffer, 0, count));
    }

    [RedisAclFact]
    public async Task ViewerCanBrowseCacheButCannotWriteDeleteChangeExpiryExecuteScriptsOrPublish()
    {
        using var admin = await ConnectionMultiplexer.ConnectAsync(Settings("ADMIN"));
        using var viewer = await ConnectionMultiplexer.ConnectAsync(Settings("VIEWER"));
        var key = "user-session-state:acl-test:" + Guid.NewGuid().ToString("N");
        var database = admin.GetDatabase();
        await database.HashSetAsync(key, "data", "read-only-test");
        await database.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
        try
        {
            var read = viewer.GetDatabase();
            Assert.Equal("read-only-test", (string?)await read.HashGetAsync(key, "data"));
            Assert.NotNull(await read.KeyTimeToLiveAsync(key));
            Assert.NotEmpty(await read.HashGetAllAsync(key));
            Assert.NotNull(await read.ExecuteAsync("SCAN", "0", "MATCH", key));
            await DeniedAsync(() => read.HashSetAsync(key, "data", "changed"));
            await DeniedAsync(() => read.KeyDeleteAsync(key));
            await DeniedAsync(() => read.KeyExpireAsync(key, TimeSpan.FromDays(1)));
            await DeniedAsync(() => read.ScriptEvaluateAsync("return 1"));
            await DeniedAsync(() => viewer.GetSubscriber().PublishAsync(RedisChannel.Literal("ecafe:signalr:test"), "forbidden"));
            foreach (var command in new[] { "FLUSHALL", "FLUSHDB", "CONFIG", "ACL", "SHUTDOWN" })
            {
                var arguments = command switch
                {
                    "CONFIG" => new object[] { "SET", "maxmemory", "0" },
                    "ACL" => new object[] { "SETUSER", "must-not-exist", "on" },
                    _ => Array.Empty<object>()
                };
                var result = await database.ExecuteAsync("ACL",
                    new object[] { "DRYRUN", "ecafe_viewer", command }.Concat(arguments).ToArray());
                Assert.NotEqual("OK", result.ToString());
            }
            Assert.Equal("read-only-test", (string?)await database.HashGetAsync(key, "data"));
        }
        finally { await database.KeyDeleteAsync(key); }
    }

    [RedisAclFact]
    public async Task RuntimeUsersCannotReadOtherNamespacesOrAdministerRedis()
    {
        using var cache = await ConnectionMultiplexer.ConnectAsync(Settings("CACHE"));
        using var realtime = await ConnectionMultiplexer.ConnectAsync(Settings("REALTIME"));
        using var admin = await ConnectionMultiplexer.ConnectAsync(Settings("ADMIN"));
        var session = "user-session-state:acl-test:" + Guid.NewGuid().ToString("N");
        var lease = "ecafe:realtime:connections:{acl-test-" + Guid.NewGuid().ToString("N") + "}";
        try
        {
            await cache.GetDatabase().HashSetAsync(session, "data", "session");
            await realtime.GetDatabase().SortedSetAddAsync(lease, "test-lease", 1);
            await DeniedAsync(() => realtime.GetDatabase().HashGetAsync(session, "data"));
            await DeniedAsync(() => realtime.GetDatabase().ExecuteAsync("DEL", session));
            await DeniedAsync(() => cache.GetDatabase().KeyDeleteAsync(lease));
            await DeniedAsync(() => cache.GetDatabase().HashSetAsync("unrelated:application", "data", "no"));
            await DeniedAsync(() => realtime.GetSubscriber().PublishAsync(RedisChannel.Literal("unrelated:application"), "no"));
            foreach (var connection in new[] { cache, realtime })
            {
                await DeniedAsync(() => connection.GetDatabase().ExecuteAsync("CONFIG", "GET", "*"));
                await DeniedAsync(() => connection.GetDatabase().ExecuteAsync("ACL", "USERS"));
            }
        }
        finally { await admin.GetDatabase().KeyDeleteAsync([session, lease]); }
    }

    [RedisAclFact]
    public async Task CacheReconnectReauthenticatesWithoutLosingExistingData()
    {
        using var admin = await ConnectionMultiplexer.ConnectAsync(Settings("ADMIN"));
        using var cache = await ConnectionMultiplexer.ConnectAsync(Settings("CACHE"));
        var database = cache.GetDatabase();
        var key = "user-session-state:acl-reconnect:" + Guid.NewGuid().ToString("N");
        try
        {
            await database.HashSetAsync(key, "data", "still-active");
            await database.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
            var id = (long)await database.ExecuteAsync("CLIENT", "ID");
            Assert.Equal(1, (long)await admin.GetDatabase().ExecuteAsync("CLIENT", "KILL", "ID", id));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                try
                {
                    Assert.Equal("still-active", (string?)await database.HashGetAsync(key, "data"));
                    break;
                }
                catch (RedisException) { await Task.Delay(100, timeout.Token); }
            }
        }
        finally { await admin.GetDatabase().KeyDeleteAsync(key); }
    }

    [RedisAclFact]
    public async Task AuthenticatedCacheCannotOverrideFreshSessionStateAndSupportsLegacyCleanup()
    {
        var options = Options.Create(new RedisCacheOptions { Configuration = Settings("CACHE").ToString(includePassword: true) });
        using var cache = new RedisCache(options);
        var userId = Random.Shared.Next(1000000, int.MaxValue);
        var sessionId = Guid.NewGuid().ToString("N");
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetSessionStateAsync(userId)).ReturnsAsync((true, 7, true));
        var refreshTokens = new Mock<IUserRefreshTokenRepository>(MockBehavior.Strict);
        refreshTokens.Setup(repository => repository.HasActiveUserSessionAsync(userId, sessionId, It.IsAny<DateTime>())).ReturnsAsync(true);
        var service = new UserSessionStateCache(cache, users.Object, refreshTokens.Object,
            new ConfigurationBuilder().Build(), NullLogger<UserSessionStateCache>.Instance);
        try
        {
            for (var index = 0; index < 3; index++)
            {
                Assert.Equal(7, (await service.GetAsync(userId))!.SessionVersion);
                Assert.True(await service.IsSessionActiveAsync(userId, sessionId));
            }
            users.Verify(repository => repository.GetSessionStateAsync(userId), Times.Exactly(3));
            refreshTokens.Verify(repository => repository.HasActiveUserSessionAsync(userId, sessionId, It.IsAny<DateTime>()), Times.Exactly(3));
            await service.InvalidateAsync(userId);
            await service.InvalidateSessionAsync(userId, sessionId);
            users.Setup(repository => repository.GetSessionStateAsync(userId)).ReturnsAsync((true, 8, true));
            refreshTokens.Setup(repository => repository.HasActiveUserSessionAsync(userId, sessionId, It.IsAny<DateTime>())).ReturnsAsync(false);
            Assert.Equal(8, (await service.GetAsync(userId))!.SessionVersion);
            Assert.False(await service.IsSessionActiveAsync(userId, sessionId));
            users.Verify(repository => repository.GetSessionStateAsync(userId), Times.Exactly(4));
            refreshTokens.Verify(repository => repository.HasActiveUserSessionAsync(userId, sessionId, It.IsAny<DateTime>()), Times.Exactly(4));
            var slidingKey = "role_permissions_v3_acl-test-" + Guid.NewGuid().ToString("N");
            await cache.SetStringAsync(slidingKey, "[1,2]", new DistributedCacheEntryOptions
                { SlidingExpiration = TimeSpan.FromSeconds(30), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });
            Assert.Equal("[1,2]", await cache.GetStringAsync(slidingKey));
            await cache.RefreshAsync(slidingKey);
            await cache.RemoveAsync(slidingKey);
            Assert.Null(await cache.GetStringAsync(slidingKey));
        }
        finally { await service.InvalidateAsync(userId); await service.InvalidateSessionAsync(userId, sessionId); }
    }

    /// <summary>Yalnız ayrıca lokal Redis-i qəbul edir; canlı serverdə test əmri işləməsini bloklayır.</summary>
    internal static ConfigurationOptions Settings(string role)
    {
        var configuration = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("ECAFE_TEST_REDIS_ACL_" + role)!);
        foreach (var endpoint in configuration.EndPoints)
        {
            var host = endpoint switch { DnsEndPoint dns => dns.Host, IPEndPoint ip => ip.Address.ToString(), _ => "" };
            if (host != "localhost" && (!IPAddress.TryParse(host, out var address) || !IPAddress.IsLoopback(address)))
                throw new InvalidOperationException("ACL tests require an isolated loopback Redis.");
        }
        configuration.AllowAdmin = true;
        return configuration;
    }

    /// <summary>Əmrin şəbəkə xətasına görə deyil, məhz Redis icazəsinə görə rədd edildiyini təsdiqləyir.</summary>
    private static async Task DeniedAsync(Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<RedisServerException>(action);
        Assert.Contains("NOPERM", error.Message);
    }
}
