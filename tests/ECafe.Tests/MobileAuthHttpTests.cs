using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using ECafe.Api.Controllers;
using ECafe.Api.Middlewares;
using ECafe.Api.Security;
using ECafe.Application.Common.Errors;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Features.Commands.Auth.Login;
using ECafe.Application.Features.Commands.Auth.MobileLogout;
using ECafe.Application.Features.Commands.Auth.Refresh;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Monitoring.Abstract;
using ECafe.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class MobileAuthHttpTests
{
    [Fact]
    public async Task HttpsPipelinePreservesWebContractNativeBodyIsolationNoStoreAndFailureStatuses()
    {
        await using var host = await Host.StartAsync();
        using var login = await host.Client.PostAsJsonAsync("/api/v1/mobile/auth/login", new { email = "test@example.test", password = "test-password" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(login.Headers.CacheControl?.NoStore);
        Assert.False(login.Headers.Contains("Set-Cookie"));
        using var result = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.Equal("test-access", result.RootElement.GetProperty("accessToken").GetString());
        Assert.Equal("test-refresh", result.RootElement.GetProperty("refreshToken").GetString());

        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/mobile/auth/refresh")
            { Content = JsonContent.Create(new { refreshToken = "body-token" }) };
        refresh.Headers.TryAddWithoutValidation("Authorization", "Bearer stale-test-token");
        refresh.Headers.TryAddWithoutValidation("Cookie", "ecafe_refresh_token=must-not-be-used");
        using var refreshed = await host.Client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.True(refreshed.Headers.CacheControl?.NoStore);
        Assert.False(refreshed.Headers.Contains("Set-Cookie"));

        using var cookieOnly = new HttpRequestMessage(HttpMethod.Post, "/api/v1/mobile/auth/refresh")
            { Content = JsonContent.Create(new { refreshToken = "" }) };
        cookieOnly.Headers.TryAddWithoutValidation("Cookie", "ecafe_refresh_token=must-not-be-used");
        using var denied = await host.Client.SendAsync(cookieOnly);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.DoesNotContain("must-not-be-used", await denied.Content.ReadAsStringAsync());
        Assert.False(denied.Headers.Contains("Set-Cookie"));

        using var reused = await host.Client.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new { refreshToken = "reused-test-token" });
        Assert.Equal(HttpStatusCode.Forbidden, reused.StatusCode);
        using var transient = await host.Client.PostAsJsonAsync("/api/v1/mobile/auth/refresh", new { refreshToken = "transient-test-token" });
        Assert.Equal(HttpStatusCode.InternalServerError, transient.StatusCode);
        Assert.DoesNotContain("transient-test-token", await transient.Content.ReadAsStringAsync());

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/mobile/auth/logout")
            { Content = JsonContent.Create(new { refreshToken = "old-body-token" }) };
        logout.Headers.TryAddWithoutValidation("Authorization", "Bearer stale-test-token");
        using var loggedOut = await host.Client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, loggedOut.StatusCode);
        Assert.False(loggedOut.Headers.Contains("Set-Cookie"));

        using var webLogin = await host.Client.PostAsJsonAsync("/api/v1/user/login", new { email = "test@example.test", password = "test-password" });
        Assert.Equal(HttpStatusCode.OK, webLogin.StatusCode);
        Assert.True(webLogin.Headers.Contains("Set-Cookie"));
        using var webResult = JsonDocument.Parse(await webLogin.Content.ReadAsStringAsync());
        Assert.False(webResult.RootElement.TryGetProperty("refreshToken", out _));
        using var webRefresh = await host.Client.PostAsJsonAsync("/api/v1/user/refresh", new { refreshToken = "body-token" });
        Assert.Equal(HttpStatusCode.OK, webRefresh.StatusCode);
        Assert.True(webRefresh.Headers.Contains("Set-Cookie"));
        using var webRefreshResult = JsonDocument.Parse(await webRefresh.Content.ReadAsStringAsync());
        Assert.False(webRefreshResult.RootElement.TryGetProperty("refreshToken", out _));
    }

    [Fact]
    public async Task NativeAndWebLoginShareTheExistingRateLimitBudget()
    {
        await using var host = await Host.StartAsync(loginLimit: 2);
        using var first = await host.Client.PostAsJsonAsync("/api/v1/user/login", new { email = "test@example.test", password = "test-password" });
        using var second = await host.Client.PostAsJsonAsync("/api/v1/mobile/auth/login", new { email = "test@example.test", password = "test-password" });
        using var limited = await host.Client.PostAsJsonAsync("/api/v1/mobile/auth/login", new { email = "test@example.test", password = "test-password" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.False(limited.Headers.Contains("Set-Cookie"));
        using var error = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        Assert.Equal("TooManyRequests", error.RootElement.GetProperty("code").GetString());
    }

    private sealed class Host(WebApplication app, X509Certificate2 certificate, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        /// <summary>Yalnız loopback-da müvəqqəti self-signed HTTPS serveri açır; real auth DB-si və canlı servis istifadə olunmur.</summary>
        public static async Task<Host> StartAsync(int loginLimit = 100)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory
            });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AuthLogin:PermitLimit"] = loginLimit.ToString()
            });
            builder.Services.AddControllers().AddApplicationPart(typeof(MobileAuthController).Assembly);
            builder.Services.AddAuthorization();
            builder.Services.AddEcafeRateLimiting(builder.Configuration);
            builder.Services.AddSingleton<IErrorMessageProvider>(new ErrorMessageProvider());
            builder.Services.AddSingleton(Mock.Of<ICriticalEventReporter>());
            builder.Services.AddSingleton(Mock.Of<IUserSessionStateCache>());
            var mediator = new Mock<IMediator>(MockBehavior.Strict);
            var auth = new AuthResponseDto { AccessToken = "test-access", RefreshToken = "test-refresh" };
            mediator.Setup(m => m.Send(It.IsAny<LoginUserCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(auth);
            mediator.Setup(m => m.Send(It.Is<RefreshTokenCommand>(c => c.RefreshToken == "body-token"), It.IsAny<CancellationToken>())).ReturnsAsync(auth);
            mediator.Setup(m => m.Send(It.Is<RefreshTokenCommand>(c => c.RefreshToken == ""), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedException(ErrorCode.RefreshTokenInvalid));
            mediator.Setup(m => m.Send(It.Is<RefreshTokenCommand>(c => c.RefreshToken == "reused-test-token"), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ForbiddenException(ErrorCode.RefreshTokenReuseDetected));
            mediator.Setup(m => m.Send(It.Is<RefreshTokenCommand>(c => c.RefreshToken == "transient-test-token"), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Synthetic transient auth test failure"));
            mediator.Setup(m => m.Send(It.Is<MobileLogoutCommand>(c => c.RefreshToken == "old-body-token"), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            builder.Services.AddSingleton(mediator.Object);
            var app = builder.Build();
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseRouting();
            app.Use(async (context, next) =>
            {
                if (context.Request.Headers.ContainsKey("Authorization"))
                    context.User = new(new ClaimsIdentity([new("userId", "1001")], "stale-test-access"));
                await next();
            });
            app.UseMiddleware<ActiveUserMiddleware>();
            app.UseRateLimiter();
            app.UseAuthorization();
            app.MapControllers();
            try
            {
                await app.StartAsync();
                var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                var handler = new HttpClientHandler
                {
                    UseProxy = false,
                    UseCookies = false,
                    ServerCertificateCustomValidationCallback = (_, actual, _, _) => actual?.Thumbprint == certificate.Thumbprint
                };
                return new(app, certificate, new HttpClient(handler) { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(15) });
            }
            catch { await app.DisposeAsync(); certificate.Dispose(); throw; }
        }

        /// <summary>Bu testin HTTPS serverini və yalnız ona aid sertifikat/HTTP klientini bağlayır.</summary>
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
            certificate.Dispose();
        }
    }
}
