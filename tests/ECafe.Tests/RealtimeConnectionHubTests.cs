using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ECafe.Api.Realtime;
using ECafe.Api.Realtime.Connections.Abstract;
using ECafe.Api.Services.Realtime;
using ECafe.Application.Common.Errors;
using ECafe.Application.Services.Realtime;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Xunit;

namespace ECafe.Tests;

public sealed class RealtimeConnectionHubTests
{
    private const string TestKey = "local-realtime-limit-test-signing-key-only-1234567890";

    [Fact]
    public async Task ExcessSocketIsClosedWithoutInterruptingExistingNotificationsAndReconnect()
    {
        await using var app = await StartAsync(limit: 2);
        await using var first = await Client.ConnectAsync(app, 10);
        await using var second = await Client.ConnectAsync(app, 10);
        await using var extra = await Client.ConnectAsync(app, 10);
        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.False(extra.Accepted);

        var notifier = Notifier(app);
        await notifier.NotifyOrderChangedAsync([10], Change());
        Assert.Equal("OrderChanged", (await first.InvocationAsync()).GetProperty("target").GetString());
        Assert.Equal("OrderChanged", (await second.InvocationAsync()).GetProperty("target").GetString());

        first.Abort();
        await WaitForSlotAsync(app, 10);
        await using var restored = await Client.ConnectAsync(app, 10);
        Assert.True(restored.Accepted);
        await notifier.NotifyNotificationsChangedAsync(10);
        Assert.Equal("NotificationsChanged", (await second.InvocationAsync()).GetProperty("target").GetString());
        Assert.Equal("NotificationsChanged", (await restored.InvocationAsync()).GetProperty("target").GetString());
    }

    [Fact]
    public async Task ParallelWebSocketsCannotBypassTheLimitOrTakeAnotherUsersSlots()
    {
        await using var app = await StartAsync(limit: 3);
        var clients = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Client.ConnectAsync(app, 10)));
        try
        {
            Assert.Equal(3, clients.Count(client => client.Accepted));
            await using var otherUser = await Client.ConnectAsync(app, 20);
            Assert.True(otherUser.Accepted);
            await Notifier(app).NotifyOrderChangedAsync([10], Change());
            foreach (var client in clients.Where(client => client.Accepted))
                Assert.Equal("OrderChanged", (await client.InvocationAsync()).GetProperty("target").GetString());
            using var timeout = new CancellationTokenSource(200);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => otherUser.InvocationAsync(timeout.Token));
        }
        finally { foreach (var client in clients) await client.DisposeAsync(); }
    }

    [Fact]
    public async Task MessagePackHubProtocolRemainsDisabled()
    {
        await using var app = await StartAsync(limit: 1);
        await using var unsupported = await Client.ConnectAsync(app, 10, protocol: "messagepack");
        Assert.False(unsupported.Accepted);
        await using var json = await Client.ConnectAsync(app, 10);
        Assert.True(json.Accepted);
        await Notifier(app).NotifyOrderChangedAsync([10], Change());
        Assert.Equal("OrderChanged", (await json.InvocationAsync()).GetProperty("target").GetString());
    }

    [Fact]
    public async Task LongPollingRequestsShareOneLogicalSlotAndDeleteReleasesIt()
    {
        await using var app = await StartAsync(limit: 1);
        var address = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(10) };
        var authentication = "access_token=" + Uri.EscapeDataString(Token(10));
        using var negotiated = await http.PostAsync("/hubs/user-events/negotiate?negotiateVersion=1&" + authentication, null);
        negotiated.EnsureSuccessStatusCode();
        using var data = JsonDocument.Parse(await negotiated.Content.ReadAsStringAsync());
        var path = "/hubs/user-events?id=" + Uri.EscapeDataString(data.RootElement.GetProperty("connectionToken").GetString()!) + "&" + authentication;
        using (var initial = await http.GetAsync(path)) initial.EnsureSuccessStatusCode();
        try
        {
            await PostFrameAsync(http, path, "{\"protocol\":\"json\",\"version\":1}");
            var handshake = await http.GetStringAsync(path);
            Assert.Contains("{}", handshake);
            await PostFrameAsync(http, path, "{\"type\":1,\"invocationId\":\"probe\",\"target\":\"__connection_probe\",\"arguments\":[]}");
            Assert.Contains("probe", await http.GetStringAsync(path));
            await using var extra = await Client.ConnectAsync(app, 10);
            Assert.False(extra.Accepted);
            for (var i = 0; i < 3; i++)
            {
                await PostFrameAsync(http, path, "{\"type\":6}");
                await Notifier(app).NotifyNotificationsChangedAsync(10);
                Assert.Contains("NotificationsChanged", await http.GetStringAsync(path));
            }
        }
        finally
        {
            using var deleted = await http.DeleteAsync(path);
            deleted.EnsureSuccessStatusCode();
        }
        await WaitForSlotAsync(app, 10);
        await using var restored = await Client.ConnectAsync(app, 10);
        Assert.True(restored.Accepted);
    }

    [RealtimeRedisFact]
    public async Task TwoRealApiInstancesShareTheLimitAndStillDeliverRedisBackplaneMessages()
    {
        var endpoint = Environment.GetEnvironmentVariable("ECAFE_TEST_REDIS")!;
        var channel = "ecafe:test:" + Guid.NewGuid().ToString("N");
        var userId = Random.Shared.Next(1000000, int.MaxValue - 1);
        await using var firstApi = await StartAsync(2, endpoint, channel);
        await using var secondApi = await StartAsync(2, endpoint, channel);
        await using var first = await Client.ConnectAsync(firstApi, userId);
        await using var second = await Client.ConnectAsync(secondApi, userId);
        await using var denied = await Client.ConnectAsync(secondApi, userId);
        await using var foreign = await Client.ConnectAsync(secondApi, userId + 1);
        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.False(denied.Accepted);
        Assert.True(foreign.Accepted);

        var change = Change();
        await Notifier(firstApi).NotifyOrderChangedAsync([userId], change);
        foreach (var client in new[] { first, second })
            Assert.Equal(change.EventId.ToString(), (await client.InvocationAsync()).GetProperty("arguments")[0].GetProperty("eventId").GetString());
        using (var timeout = new CancellationTokenSource(200))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => foreign.InvocationAsync(timeout.Token));

        first.Abort();
        await WaitForSlotAsync(secondApi, userId);
        await using var restored = await Client.ConnectAsync(secondApi, userId);
        Assert.True(restored.Accepted);
        await Notifier(firstApi).NotifyNotificationsChangedAsync(userId);
        Assert.Equal("NotificationsChanged", (await restored.InvocationAsync()).GetProperty("target").GetString());
        Assert.Equal("NotificationsChanged", (await second.InvocationAsync()).GetProperty("target").GetString());
    }

    /// <summary>JWT, real hub filteri və istəyə görə real Redis backplane ilə yalnız lokal portda test API-si qaldırır.</summary>
    private static async Task<WebApplication> StartAsync(int limit, string? redis = null, string? channel = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SignalR:ConnectionLimits:MaxConnectionsPerUser"] = limit.ToString(),
            ["SignalR:UseRedisBackplane"] = (redis != null).ToString(),
            ["SignalR:Redis:Connection"] = redis
        });
        builder.Services.AddRealtimeConnectionLimits(builder.Configuration);
        builder.Services.AddSingleton<IErrorMessageProvider>(new ErrorMessageProvider());
        var signalR = builder.Services.AddSignalR(options => options.AddFilter<RealtimeConnectionLimitFilter>());
        if (redis != null) signalR.AddStackExchangeRedis(redis, options => options.Configuration.ChannelPrefix = RedisChannel.Literal(channel!));
        builder.Services.AddSingleton<IUserIdProvider, UserIdProvider>();
        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey))
            };
            options.Events = new JwtBearerEvents { OnMessageReceived = context =>
            {
                context.Token = context.Request.Query["access_token"];
                return Task.CompletedTask;
            } };
        });
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHub<UserEventsHub>("/hubs/user-events");
        try { await app.StartAsync(); return app; }
        catch { await app.DisposeAsync(); throw; }
    }

    /// <summary>İstehsal notifier-ini həmin test API-sinin hub kontekstinə bağlayır.</summary>
    private static UserRealtimeNotifier Notifier(WebApplication app) => new(app.Services.GetRequiredService<IHubContext<UserEventsHub>>());

    /// <summary>Məxfi məlumat olmayan sifariş scope-u ilə standart real vaxt hadisəsi yaradır.</summary>
    private static OrderChangedEvent Change() => new(Guid.NewGuid(), 1, 11, 100, DateTime.UtcNow);

    /// <summary>HTTP və WebSocket testləri üçün eyni lokal imzalanmış kimliyi yaradır.</summary>
    private static string Token(int userId)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims: [new Claim("userId", userId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials));
    }

    /// <summary>Long-polling bağlantısında ayrıca HTTP POST-un yeni məntiqi bağlantı yaratmadığını yoxlamaq üçün kadr göndərir.</summary>
    private static async Task PostFrameAsync(HttpClient http, string path, string frame)
    {
        using var body = new StringContent(frame + '\u001e', Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(path, body);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Disconnect-in həqiqətən rezervi buraxdığını sabit sleep əvəzinə atomik test rezervi ilə gözləyir.</summary>
    private static async Task WaitForSlotAsync(WebApplication app, int userId)
    {
        var store = app.Services.GetRequiredService<IRealtimeConnectionLeaseStore>();
        var probe = Guid.NewGuid().ToString("N");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!await store.TryAcquireAsync(userId, probe, timeout.Token)) await Task.Delay(20, timeout.Token);
        await store.ReleaseAsync(userId, probe);
    }

    private sealed class Client : IAsyncDisposable
    {
        private readonly ClientWebSocket _socket = new();
        private string _buffer = "";
        public bool Accepted { get; private set; }

        /// <summary>JSON handshake-dən sonra mövcud olmayan metodun completion-unu gözləyərək hub qəbulunun bitməsini təsdiqləyir.</summary>
        public static async Task<Client> ConnectAsync(WebApplication app, int userId, string protocol = "json")
        {
            var client = new Client();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                var token = Token(userId);
                var address = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                var uri = new UriBuilder(address) { Scheme = "ws", Path = "/hubs/user-events", Query = "access_token=" + Uri.EscapeDataString(token) }.Uri;
                await client._socket.ConnectAsync(uri, timeout.Token);
                await client.SendAsync(JsonSerializer.Serialize(new { protocol, version = 1 }), timeout.Token);
                var handshake = await client.FrameAsync(timeout.Token);
                if (handshake == null || handshake.Value.TryGetProperty("error", out _)) return client;
                await client.SendAsync("{\"type\":1,\"invocationId\":\"probe\",\"target\":\"__connection_probe\",\"arguments\":[]}", timeout.Token);
                while (await client.FrameAsync(timeout.Token) is { } frame)
                {
                    if (!frame.TryGetProperty("type", out var type)) continue;
                    if (type.GetInt32() == 7) return client;
                    if (type.GetInt32() == 3) { client.Accepted = true; return client; }
                }
                return client;
            }
            catch { client._socket.Dispose(); throw; }
        }

        /// <summary>Bir lokal test paketini SignalR record separator ilə göndərir.</summary>
        private Task SendAsync(string message, CancellationToken token)
            => _socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message + '\u001e')), WebSocketMessageType.Text, true, token);

        /// <summary>Ping-ləri keçib yalnız notifier hadisəsini qaytarır.</summary>
        public async Task<JsonElement> InvocationAsync(CancellationToken token = default)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            while (await FrameAsync(timeout.Token) is { } frame)
                if (frame.TryGetProperty("type", out var type) && type.GetInt32() == 1) return frame;
            throw new InvalidOperationException("Connection closed before the expected event.");
        }

        /// <summary>Eyni WebSocket paketində gələn handshake və close mesajlarını itirmədən bir-bir oxuyur.</summary>
        private async Task<JsonElement?> FrameAsync(CancellationToken token)
        {
            var bytes = new byte[8192];
            while (true)
            {
                var separator = _buffer.IndexOf('\u001e');
                if (separator >= 0)
                {
                    using var document = JsonDocument.Parse(_buffer[..separator]);
                    _buffer = _buffer[(separator + 1)..];
                    return document.RootElement.Clone();
                }
                var read = await _socket.ReceiveAsync(new ArraySegment<byte>(bytes), token);
                if (read.MessageType == WebSocketMessageType.Close) return null;
                _buffer += Encoding.UTF8.GetString(bytes, 0, read.Count);
            }
        }

        /// <summary>Telefonda və ya brauzerdə qəfil şəbəkə kəsilməsini yalnız lokal test bağlantısında modelləşdirir.</summary>
        public void Abort() => _socket.Abort();

        /// <summary>Testdən sonra normal close handshake edir; yararsız soketi məhdud gözləmədən sonra buraxır.</summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", timeout.Token);
                }
            }
            catch (Exception error) when (error is WebSocketException or OperationCanceledException) { _socket.Abort(); }
            finally { _socket.Dispose(); }
        }
    }
}
