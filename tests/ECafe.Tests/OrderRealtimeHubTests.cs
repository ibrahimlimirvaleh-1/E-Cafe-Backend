using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ECafe.Api.Realtime;
using ECafe.Api.Services.Realtime;
using ECafe.Application.Services.Realtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ECafe.Tests;

public sealed class OrderRealtimeHubTests
{
    private const string TestKey = "local-realtime-integration-test-key-only-1234567890";

    [Fact]
    public async Task RealSignalRWebSocketsDeliverOnlyToSelectedUsersAndReconnectWithTheSameIdentity()
    {
        await using var app = await StartAsync();
        using var waiter = await ConnectAsync(app, 10);
        using var kitchen = await ConnectAsync(app, 20);
        using var foreignRestaurant = await ConnectAsync(app, 30);
        var notifier = new UserRealtimeNotifier(app.Services.GetRequiredService<IHubContext<UserEventsHub>>());
        var change = new OrderChangedEvent(Guid.NewGuid(), 1, 11, 100, DateTime.UtcNow);
        await notifier.NotifyOrderChangedAsync(new[] { 10, 20 }, change);
        foreach (var socket in new[] { waiter, kitchen })
        {
            var payload = await ReceiveInvocationAsync(socket);
            Assert.Equal("OrderChanged", payload.GetProperty("target").GetString());
            var argument = payload.GetProperty("arguments")[0];
            Assert.Equal(change.EventId.ToString(), argument.GetProperty("eventId").GetString());
            Assert.Equal(1, argument.GetProperty("restaurantId").GetInt32());
            Assert.Equal(100, argument.GetProperty("orderId").GetInt32());
            Assert.False(argument.TryGetProperty("statusId", out _));
            Assert.False(argument.TryGetProperty("message", out _));
        }
        using (var timeout = new CancellationTokenSource(400))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReceiveInvocationAsync(foreignRestaurant, timeout.Token));
        waiter.Abort();
        using var restored = await ConnectAsync(app, 10);
        var latest = change with { EventId = Guid.NewGuid() };
        await notifier.NotifyOrderChangedAsync(new[] { 10 }, latest);
        Assert.Equal(latest.EventId.ToString(), (await ReceiveInvocationAsync(restored))
            .GetProperty("arguments")[0].GetProperty("eventId").GetString());
    }

    [Fact]
    public async Task RealHubRejectsAnUnauthenticatedWebSocket()
    {
        await using var app = await StartAsync();
        using var socket = new ClientWebSocket();
        await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(SocketUri(app), CancellationToken.None));
    }

    /// <summary>Yalnız loopback-da təsadüfi port və test JWT açarı ilə istehsaldakı hub/notifier-i qaldırır; layihə bazasına qoşulmur.</summary>
    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IUserIdProvider, UserIdProvider>();
        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = false, ValidateAudience = false,
                ValidateIssuerSigningKey = true, ValidateLifetime = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)) };
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
        await app.StartAsync();
        return app;
    }

    /// <summary>JWT userId-si ilə həqiqi WebSocket və SignalR JSON handshake qurur.</summary>
    private static async Task<ClientWebSocket> ConnectAsync(WebApplication app, int userId)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims: [new Claim("userId", userId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials));
        var socket = new ClientWebSocket();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await socket.ConnectAsync(SocketUri(app, token), timeout.Token);
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, timeout.Token);
            var buffer = new byte[4096];
            await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
            await Task.Delay(30, timeout.Token);
            return socket;
        }
        catch { socket.Dispose(); throw; }
    }

    /// <summary>Serverin yaratdığı lokal portdan hub ünvanını alır; heç bir xarici hosta çıxmır.</summary>
    private static Uri SocketUri(WebApplication app, string? token = null)
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        var uri = new UriBuilder(addresses.Addresses.Single()) { Scheme = "ws", Path = "/hubs/user-events",
            Query = token == null ? "" : "access_token=" + Uri.EscapeDataString(token) };
        return uri.Uri;
    }

    /// <summary>Ping-ləri keçərək ilk SignalR invocation-u oxuyur; testin asılı qalmaması üçün sərhədli gözləyir.</summary>
    private static async Task<JsonElement> ReceiveInvocationAsync(ClientWebSocket socket, CancellationToken token = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var buffer = new byte[8192];
        var text = "";
        while (true)
        {
            var read = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
            text += Encoding.UTF8.GetString(buffer, 0, read.Count);
            var separator = text.IndexOf('\u001e');
            while (separator >= 0)
            {
                using var json = JsonDocument.Parse(text[..separator]);
                if (json.RootElement.TryGetProperty("type", out var type) && type.GetInt32() == 1) return json.RootElement.Clone();
                text = text[(separator + 1)..];
                separator = text.IndexOf('\u001e');
            }
        }
    }
}
