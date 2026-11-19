using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECafe.Api.Middlewares;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Redis;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;
using Role = ECafe.Domain.Entities.Role;
using File = System.IO.File;

namespace ECafe.Tests;

public sealed class PermissionRedisLifecycleFactAttribute : FactAttribute
{
    /// <summary>Redis-in dayandırılmasına yalnız açıq şəkildə seçilmiş, testin özünün yaratdığı lokal konteynerdə icazə verir.</summary>
    public PermissionRedisLifecycleFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ECAFE_TEST_PERMISSION_REDIS_LIFECYCLE") != "1")
            Skip = "Set ECAFE_TEST_PERMISSION_REDIS_LIFECYCLE=1 to create and stop a disposable local Redis.";
    }
}

public sealed class PermissionCacheRedisRecoveryTests
{
    [PermissionRedisLifecycleFact]
    public async Task PermissionsAndSessionGuardsSurviveARealRedisStopAndRecoverAfterRestart()
    {
        await using var redis = await IsolatedRedis.StartAsync();
        using var cache = new RedisCache(Options.Create(new RedisCacheOptions
        {
            ConfigurationOptions = redis.Connection,
            ConnectionMultiplexerFactory = async () => await ConnectionMultiplexer.ConnectAsync(redis.Connection)
        }));
        var roleId = Random.Shared.Next(1000000, int.MaxValue - 1);
        var permissionId = roleId + 1;
        await using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new PermissionCacheInvalidationInterceptor(cache)).Options);
        await db.Database.EnsureCreatedAsync();
        var grant = new RolePermission
        {
            Id = roleId, Role = new Role { Id = roleId, Name = "Redis recovery role" },
            Permission = new Permission { Id = permissionId, Name = "Redis recovery permission" }
        };
        db.RolePermissions.Add(grant);
        await db.SaveChangesAsync();
        var permissions = new PermissionCacheService(cache, db, NullLogger<PermissionCacheService>.Instance);
        Assert.Equal([permissionId], await permissions.GetPermissionsAsync(roleId));
        await cache.SetStringAsync(PermissionCacheKeys.ForRole(roleId), JsonSerializer.Serialize(new[] { permissionId }));
        await cache.SetStringAsync("user-session-state:17", JsonSerializer.Serialize(new UserSessionState(true, 7)));
        await cache.SetStringAsync("user-session-active:17:isolated-test-session", "True");

        (bool IsActive, int SessionVersion)? currentUser = (true, 7);
        var activeSession = true;
        var users = new Mock<IUserRepository>();
        users.Setup(value => value.GetSessionStateAsync(17)).ReturnsAsync(() => currentUser);
        var refresh = new Mock<IUserRefreshTokenRepository>();
        refresh.Setup(value => value.HasActiveUserSessionAsync(17, "isolated-test-session", It.IsAny<DateTime>()))
            .ReturnsAsync(() => activeSession);
        var sessions = new UserSessionStateCache(cache, users.Object, refresh.Object,
            new ConfigurationBuilder().Build(), NullLogger<UserSessionStateCache>.Instance);
        var reachedNext = false;
        var middleware = new ActiveUserMiddleware(_ => { reachedNext = true; return Task.CompletedTask; });
        var http = AuthenticatedRequest();
        await middleware.Invoke(http, sessions);
        Assert.True(reachedNext);

        await redis.StopAsync();
        await Assert.ThrowsAnyAsync<RedisException>(() => cache.GetAsync(PermissionCacheKeys.ForRole(roleId)));
        for (var request = 0; request < 3; request++)
        {
            Assert.Equal([permissionId], await permissions.GetPermissionsAsync(roleId));
            Assert.Empty(await permissions.GetPermissionsAsync(int.MaxValue));
            reachedNext = false;
            await middleware.Invoke(http, sessions);
            Assert.True(reachedNext);
        }

        currentUser = (false, 7);
        await Assert.ThrowsAsync<UnauthorizedException>(() => middleware.Invoke(http, sessions));
        currentUser = (true, 8);
        await Assert.ThrowsAsync<UnauthorizedException>(() => middleware.Invoke(http, sessions));
        currentUser = (true, 7);
        activeSession = false;
        await Assert.ThrowsAsync<UnauthorizedException>(() => middleware.Invoke(http, sessions));
        activeSession = true;

        // Redis əlçatmaz ikən icazə bazada alınır; uğursuz təmizləmə commit-i uğursuz göstərməməlidir.
        db.RolePermissions.Remove(grant);
        await db.SaveChangesAsync();
        Assert.Empty(await permissions.GetPermissionsAsync(roleId));
        currentUser = (false, 7);
        activeSession = false;

        await redis.RestartAsync();
        await WaitForCacheAsync(cache, PermissionCacheKeys.ForRole(roleId));
        // Köhnə instansiyanın gecikmiş yazısını bərpadan sonra da modelləşdirir.
        await cache.SetStringAsync(PermissionCacheKeys.ForRole(roleId), JsonSerializer.Serialize(new[] { permissionId }));
        await cache.SetStringAsync("user-session-state:17", JsonSerializer.Serialize(new UserSessionState(true, 7)));
        await cache.SetStringAsync("user-session-active:17:isolated-test-session", "True");
        Assert.Empty(await permissions.GetPermissionsAsync(roleId));
        reachedNext = false;
        await Assert.ThrowsAsync<UnauthorizedException>(() => middleware.Invoke(http, sessions));
        Assert.False(reachedNext);
        currentUser = (true, 8);
        await Assert.ThrowsAsync<UnauthorizedException>(() => middleware.Invoke(http, sessions));
        currentUser = (true, 7);
        await Assert.ThrowsAsync<UnauthorizedException>(() => middleware.Invoke(http, sessions));

        grant.IsDeleted = false;
        await db.SaveChangesAsync();
        Assert.Null(await cache.GetStringAsync(PermissionCacheKeys.ForRole(roleId)));
        Assert.Equal([permissionId], await permissions.GetPermissionsAsync(roleId));
        activeSession = true;
        await middleware.Invoke(http, sessions);
        Assert.True(reachedNext);
    }

    /// <summary>Saxta test kimliyi ilə real middleware-i işlədir; canlı JWT, istifadəçi və refresh token götürmür.</summary>
    private static DefaultHttpContext AuthenticatedRequest()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new("userId", "17"), new("sessionVersion", "7"),
                new("sessionId", "isolated-test-session")], "isolated-test"))
        };
        context.Request.Path = "/api/v1/profile";
        return context;
    }

    /// <summary>Fallback nəticəsini bərpa ilə qarışdırmamaq üçün Redis-in özü yenidən cavab verənədək məhdud gözləyir.</summary>
    private static async Task WaitForCacheAsync(IDistributedCache cache, string key)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            try { await cache.GetAsync(key, timeout.Token); return; }
            catch (RedisException) { await Task.Delay(100, timeout.Token); }
        }
    }

    private sealed class IsolatedRedis(string name, string directory, string docker) : IAsyncDisposable
    {
        private const string OwnerLabel = "ecafe.permission-cache-test";
        private string? _containerId;
        public ConfigurationOptions Connection { get; private set; } = null!;

        /// <summary>Ayrıca təsadüfi ACL və yalnız loopback portu ilə yeni lokal Redis yaradır; xarici server ünvanı qəbul etmir.</summary>
        public static async Task<IsolatedRedis> StartAsync()
        {
            var name = "ecafe-permission-cache-test-" + Guid.NewGuid().ToString("N");
            var directory = Path.Combine(Path.GetTempPath(), name);
            var docker = Environment.GetEnvironmentVariable("ECAFE_TEST_DOCKER") ?? "docker";
            var fixture = new IsolatedRedis(name, directory, docker);
            try
            {
                Directory.CreateDirectory(directory);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                var template = FindTemplate();
                foreach (var role in new[] { "CACHE", "REALTIME", "VIEWER", "ADMIN" })
                {
                    var secret = role == "CACHE" ? password : Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                    template = template.Replace("@" + role + "_SHA256@", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant());
                }
                var acl = Path.Combine(directory, "users.acl");
                await File.WriteAllTextAsync(acl, template);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(acl, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
                var port = UnusedLoopbackPort();
                fixture._containerId = (await fixture.DockerAsync("run", "--pull", "never", "-d", "--name", name,
                    "--label", OwnerLabel + "=true", "-p", "127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + ":6379", "--mount",
                    "type=bind,source=" + acl + ",target=/etc/redis/users.acl,readonly", "redis:7.4-alpine",
                    "redis-server", "--aclfile", "/etc/redis/users.acl", "--appendonly", "no", "--save", "")).Trim();
                using var document = JsonDocument.Parse(await fixture.DockerAsync("inspect", fixture._containerId));
                var binding = document.RootElement[0].GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("6379/tcp")[0];
                Assert.Equal("127.0.0.1", binding.GetProperty("HostIp").GetString());
                Assert.Equal(port.ToString(CultureInfo.InvariantCulture), binding.GetProperty("HostPort").GetString());
                fixture.Connection = new ConfigurationOptions
                {
                    User = "ecafe_cache", Password = password, AbortOnConnectFail = false, ConnectRetry = 0,
                    ConnectTimeout = 1000, AsyncTimeout = 500, KeepAlive = 1, BacklogPolicy = BacklogPolicy.FailFast
                };
                fixture.Connection.EndPoints.Add(IPAddress.Loopback, int.Parse(binding.GetProperty("HostPort").GetString()!, CultureInfo.InvariantCulture));
                using var ready = await ConnectionMultiplexer.ConnectAsync(fixture.Connection);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while (!ready.IsConnected) await Task.Delay(100, timeout.Token);
                await ready.GetDatabase().PingAsync();
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        /// <summary>Yalnız bu fixture-in yaratdığı və etiketi yoxlanmış konteyneri dayandırır.</summary>
        public async Task StopAsync()
        {
            await ValidateOwnershipAsync();
            await DockerAsync("stop", "--time", "1", _containerId!);
        }

        /// <summary>Eyni test konteynerini bərpa edir; istehsal Redis-inə restart əmri göndərmir.</summary>
        public async Task RestartAsync()
        {
            await ValidateOwnershipAsync();
            await DockerAsync("start", _containerId!);
            using var info = JsonDocument.Parse(await DockerAsync("inspect", _containerId!));
            var binding = info.RootElement[0].GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("6379/tcp")[0];
            Assert.Equal("127.0.0.1", binding.GetProperty("HostIp").GetString());
            Assert.Equal(((IPEndPoint)Connection.EndPoints.Single()).Port.ToString(CultureInfo.InvariantCulture), binding.GetProperty("HostPort").GetString());
        }

        /// <summary>Restartda dəyişməyən açıq port seçir; yarışda port tutulsa Docker testi dayandırır, başqa servisdən istifadə etmir.</summary>
        private static int UnusedLoopbackPort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        /// <summary>Konteyner ID-si, ad və etiket yoxlanmadan həyat dövrü əməliyyatına icazə vermir.</summary>
        private async Task ValidateOwnershipAsync()
        {
            if (string.IsNullOrWhiteSpace(_containerId)) throw new InvalidOperationException("No owned test container.");
            using var info = JsonDocument.Parse(await DockerAsync("inspect", _containerId));
            Assert.Equal("/" + name, info.RootElement[0].GetProperty("Name").GetString());
            Assert.Equal("true", info.RootElement[0].GetProperty("Config").GetProperty("Labels").GetProperty(OwnerLabel).GetString());
        }

        /// <summary>Shell və parol arqumenti olmadan Docker-i məhdud vaxtla işlədir; mühit dəyərlərini loga çıxarmır.</summary>
        private async Task<string> DockerAsync(params string[] arguments)
        {
            var start = new ProcessStartInfo(docker) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start local Docker.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
            await error;
            if (process.ExitCode != 0) throw new InvalidOperationException("Isolated test Docker operation failed: " + arguments[0]);
            return await output;
        }

        /// <summary>İstehsal ACL şablonunu tapır ki, test ayrıca və geniş icazəli qayda uydurmasın.</summary>
        private static string FindTemplate()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName, "deploy", "redis", "users.acl.template");
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            throw new FileNotFoundException("Redis ACL template is required for the lifecycle test.");
        }

        /// <summary>Yalnız öz konteynerini və əvvəlcədən sərhədi yoxlanmış müvəqqəti qovluğu təmizləyir.</summary>
        public async ValueTask DisposeAsync()
        {
            if (_containerId != null)
            {
                await ValidateOwnershipAsync();
                await DockerAsync("rm", "-f", _containerId);
                _containerId = null;
            }
            if (Directory.Exists(directory))
            {
                var target = new DirectoryInfo(Path.GetFullPath(directory));
                if (target.Parent?.FullName != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) || target.Name != name)
                    throw new InvalidOperationException("Unsafe test directory cleanup target.");
                Directory.Delete(target.FullName, recursive: true);
            }
        }
    }
}
