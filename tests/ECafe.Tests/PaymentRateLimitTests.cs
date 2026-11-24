using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ECafe.Api.Controllers;
using ECafe.Api.Security;
using ECafe.Application.Common.Errors;
using ECafe.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class PaymentRateLimitTests
{
    private const string Bill = "/api/v1/table-sessions/1001/bill";

    [Theory]
    [InlineData(typeof(PaymentFlowController), nameof(PaymentFlowController.AuthorizeDeposit), RateLimitPolicyNames.PaymentMutation)]
    [InlineData(typeof(PaymentFlowController), nameof(PaymentFlowController.CreateCheckout), RateLimitPolicyNames.PaymentMutation)]
    [InlineData(typeof(PaymentFlowController), nameof(PaymentFlowController.Simulate), RateLimitPolicyNames.PaymentMutation)]
    [InlineData(typeof(PaymentFlowController), nameof(PaymentFlowController.SimulateRefund), RateLimitPolicyNames.PaymentMutation)]
    [InlineData(typeof(PaymentFlowController), nameof(PaymentFlowController.Refresh), RateLimitPolicyNames.PaymentRefresh)]
    [InlineData(typeof(PaymentFlowController), nameof(PaymentFlowController.RefreshRefund), RateLimitPolicyNames.PaymentRefresh)]
    [InlineData(typeof(PaymentController), nameof(PaymentController.GetCapabilities), RateLimitPolicyNames.PaymentRead)]
    [InlineData(typeof(PaymentController), nameof(PaymentController.GetReservation), RateLimitPolicyNames.PaymentRead)]
    [InlineData(typeof(SessionBillController), nameof(SessionBillController.Get), RateLimitPolicyNames.PaymentRead)]
    [InlineData(typeof(SessionBillController), nameof(SessionBillController.Checkout), RateLimitPolicyNames.PaymentMutation)]
    [InlineData(typeof(SessionBillController), nameof(SessionBillController.Simulate), RateLimitPolicyNames.PaymentMutation)]
    [InlineData(typeof(SessionBillController), nameof(SessionBillController.Refresh), RateLimitPolicyNames.PaymentRefresh)]
    [InlineData(typeof(ReservationRefundFlowController), nameof(ReservationRefundFlowController.RequestRefund), RateLimitPolicyNames.PaymentMutation)]
    public void EveryActivePaymentEndpointHasCorrectPolicyAndKeepsAuthorization(Type controller, string action, string policy)
    {
        var method = controller.GetMethod(action)!;
        Assert.Equal(policy, (method.GetCustomAttribute<EnableRateLimitingAttribute>() ??
            controller.GetCustomAttribute<EnableRateLimitingAttribute>())!.PolicyName);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Theory]
    [InlineData("POST", Bill + "/checkout", 30)]
    [InlineData("POST", Bill + "/payments/1/refresh", 120)]
    [InlineData("GET", Bill, 120)]
    public async Task DefaultLimitsAreEnforcedByActualMvcEndpoints(string verb, string path, int count)
    {
        await using var host = await Host.StartAsync();
        for (var i = 0; i < count; i++)
        {
            using var accepted = await host.SendAsync(verb, path);
            Assert.True(accepted.IsSuccessStatusCode);
        }
        using var rejected = await host.SendAsync(verb, path);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(count, host.Calls);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
        using var json = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("TooManyRequests", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(429, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.True(json.RootElement.GetProperty("retryAfterSeconds").GetDouble() > 0);
    }

    [Fact]
    public async Task MutationBucketIsSharedAcrossRoutesIdsAndIpButNotAcrossUsers()
    {
        await using var host = await Host.StartAsync(limit: 2);
        using var first = await host.SendAsync("POST", Bill + "/checkout");
        using var second = await host.SendAsync("POST", "/api/v1/reservations/2020/payment/checkout", ip: "127.0.0.2");
        using var blocked = await host.SendAsync("POST", "/api/v1/payments/3030/test-outcome", ip: "127.0.0.3");
        Assert.True(first.IsSuccessStatusCode);
        Assert.True(second.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        using var other = await host.SendAsync("POST", Bill + "/checkout", user: "2002");
        Assert.True(other.IsSuccessStatusCode);
        Assert.Equal(3, host.Calls);
    }

    [Fact]
    public async Task PollingAndReadingDoNotConsumeMutationBudgetAndActionOverridesClassPolicy()
    {
        await using var host = await Host.StartAsync(limit: 1);
        using var write = await host.SendAsync("POST", Bill + "/checkout");
        using var poll = await host.SendAsync("POST", Bill + "/payments/1/refresh");
        using var read = await host.SendAsync("GET", Bill);
        Assert.True(write.IsSuccessStatusCode);
        Assert.True(poll.IsSuccessStatusCode);
        Assert.True(read.IsSuccessStatusCode);
        using var blocked = await host.SendAsync("POST", Bill + "/checkout");
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(3, host.Calls);
    }

    [Fact]
    public async Task ParallelHttpBurstCannotExceedUserMutationBudget()
    {
        await using var host = await Host.StartAsync(limit: 2);
        var responses = await Task.WhenAll(Enumerable.Range(1, 12).Select(id =>
            host.SendAsync("POST", $"/api/v1/table-sessions/{id}/bill/checkout")));
        try
        {
            Assert.Equal(2, responses.Count(response => response.IsSuccessStatusCode));
            Assert.Equal(10, responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests));
            Assert.Equal(2, host.Calls);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task AnonymousRequestsUseIpFallbackAndNeverReachBusinessHandler()
    {
        await using var host = await Host.StartAsync(limit: 1);
        using var unauthorized = await host.SendAsync("POST", Bill + "/checkout", user: null);
        using var limited = await host.SendAsync("POST", Bill + "/checkout", user: null);
        using var anotherIp = await host.SendAsync("POST", Bill + "/checkout", user: null, ip: "127.0.0.2");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anotherIp.StatusCode);
        Assert.Equal(0, host.Calls);
    }

    [Fact]
    public async Task WindowReplenishesAndLimitDoesNotChangeSuccessfulResponseSemantics()
    {
        await using var host = await Host.StartAsync(limit: 1, window: 1);
        using var first = await host.SendAsync("POST", Bill + "/checkout");
        using var blocked = await host.SendAsync("POST", Bill + "/checkout");
        Assert.True(first.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            await Task.Delay(100, timeout.Token);
            using var retry = await host.SendAsync("POST", Bill + "/checkout");
            if (retry.StatusCode == HttpStatusCode.TooManyRequests) continue;
            Assert.Equal(first.StatusCode, retry.StatusCode);
            break;
        }
        Assert.Equal(2, host.Calls);
    }

    private sealed class Host(WebApplication app, HttpClient client, Mock<IMediator> mediator) : IAsyncDisposable
    {
        public int Calls => mediator.Invocations.Count(value => value.Method.Name == "Send");

        /// <summary>Yalnız loopback-da real MVC/rate-limit pipeline açır; mediator əvəz edildiyi üçün pul və DB əməliyyatı yoxdur.</summary>
        public static async Task<Host> StartAsync(int? limit = null, int window = 60)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            var settings = new Dictionary<string, string?>();
            foreach (var policy in new[] { "PaymentMutation", "PaymentRefresh", "PaymentRead" })
            {
                if (limit.HasValue) settings[$"RateLimiting:{policy}:PermitLimit"] = limit.Value.ToString();
                settings[$"RateLimiting:{policy}:WindowSeconds"] = window.ToString();
            }
            builder.Configuration.AddInMemoryCollection(settings);
            builder.Services.AddControllers().AddApplicationPart(typeof(SessionBillController).Assembly);
            builder.Services.AddAuthentication("PaymentTest").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("PaymentTest", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddEcafeRateLimiting(builder.Configuration);
            builder.Services.AddSingleton<IErrorMessageProvider>(new ErrorMessageProvider());
            var mediator = new Mock<IMediator>();
            builder.Services.AddSingleton(mediator.Object);
            var app = builder.Build();
            app.UseRouting();
            app.Use(async (context, next) =>
            {
                if (IPAddress.TryParse(context.Request.Headers["X-Test-IP"], out var address)) context.Connection.RemoteIpAddress = address;
                await next();
            });
            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();
            app.MapControllers();
            try
            {
                await app.StartAsync();
                var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                return new(app, new HttpClient(new HttpClientHandler { UseProxy = false })
                    { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(10) }, mediator);
            }
            catch { await app.DisposeAsync(); throw; }
        }

        /// <summary>Sintetik istifadəçi/IP ilə ödəniş marşrutunu çağırır; yeni ID və açar limitdən yayınmamalıdır.</summary>
        public async Task<HttpResponseMessage> SendAsync(string verb, string path, string? user = "1001", string ip = "127.0.0.1")
        {
            using var request = new HttpRequestMessage(new HttpMethod(verb), path);
            if (user != null) request.Headers.Add("X-Test-User", user);
            request.Headers.Add("X-Test-IP", ip);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
            if (verb == "POST") request.Content = new StringContent("{\"outcome\":\"Pending\"}", Encoding.UTF8, "application/json");
            return await client.SendAsync(request);
        }

        /// <summary>Testin HTTP serverini və klientini bağlayır; başqa proseslərə toxunmur.</summary>
        public async ValueTask DisposeAsync()
        { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        /// <summary>Yalnız test assembly-sində sintetik hesab yaradır; production autentifikasiyasını dəyişmir.</summary>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(Request.Headers.TryGetValue("X-Test-User", out var user)
                ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim("userId", user.ToString()), new Claim(ClaimTypes.Role, "5") }, Scheme.Name)), Scheme.Name))
                : AuthenticateResult.NoResult());
    }
}
