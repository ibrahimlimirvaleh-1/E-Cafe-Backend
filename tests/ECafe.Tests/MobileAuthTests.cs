using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using ECafe.Api.Controllers;
using ECafe.Api.Middlewares;
using ECafe.Api.Security;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Features.Commands.Auth.Login;
using ECafe.Application.Features.Commands.Auth.MobileLogout;
using ECafe.Application.Features.Commands.Auth.Refresh;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class MobileAuthTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeLoginAndRefreshReturnBothTokensWithoutReadingOrWritingCookies(bool refresh)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var result = new AuthResponseDto { AccessToken = "test-access", RefreshToken = "test-rotated" };
        mediator.Setup(x => x.Send(It.Is<LoginUserCommand>(c => c.Email == "test@example.test"), default)).ReturnsAsync(result);
        mediator.Setup(x => x.Send(It.Is<RefreshTokenCommand>(c => c.RefreshToken == "body-only"), default)).ReturnsAsync(result);
        var context = Context(mediator.Object);
        context.Request.Headers.Cookie = "ecafe_refresh_token=cookie-must-not-win";
        var controller = new MobileAuthController { ControllerContext = new() { HttpContext = context } };
        var response = refresh
            ? await controller.Refresh(new() { RefreshToken = "body-only" })
            : await controller.Login(new() { Email = "test@example.test", Password = "test-password" });
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(response).Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("test-access", json.RootElement.GetProperty("accessToken").GetString());
        Assert.Equal("test-rotated", json.RootElement.GetProperty("refreshToken").GetString());
        Assert.Equal(2, json.RootElement.EnumerateObject().Count());
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task CookieOnlyNativeRefreshIsNotConvertedToAnAuthenticatedRefresh()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(x => x.Send(It.Is<RefreshTokenCommand>(c => c.RefreshToken == null), default))
            .ThrowsAsync(new UnauthorizedException(ErrorCode.RefreshTokenInvalid));
        var context = Context(mediator.Object);
        context.Request.Headers.Cookie = "ecafe_refresh_token=cookie-only";
        var controller = new MobileAuthController { ControllerContext = new() { HttpContext = context } };
        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => controller.Refresh(new()));
        Assert.Equal(ErrorCode.RefreshTokenInvalid, error.Code);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeLogoutUsesOnlyBodyTokenAndNeverNeedsAccessToken(bool hasBodyToken)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(x => x.Send(It.Is<MobileLogoutCommand>(c => c.RefreshToken == (hasBodyToken ? "body-only" : null)), default))
            .Returns(Task.CompletedTask);
        var context = Context(mediator.Object);
        context.Request.Headers.Cookie = "ecafe_refresh_token=cookie-must-not-be-revoked";
        var controller = new MobileAuthController { ControllerContext = new() { HttpContext = context } };
        Assert.IsType<NoContentResult>(await controller.Logout(new() { RefreshToken = hasBodyToken ? "body-only" : null! }));
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WebLoginAndRefreshStillReturnOnlyAccessTokenAndSetHttpOnlyCookie(bool refresh)
    {
        var mediator = new Mock<IMediator>();
        var auth = new AuthResponseDto { AccessToken = "test-access", RefreshToken = "test-refresh-cookie" };
        mediator.Setup(x => x.Send(It.IsAny<LoginUserCommand>(), default)).ReturnsAsync(auth);
        mediator.Setup(x => x.Send(It.IsAny<RefreshTokenCommand>(), default)).ReturnsAsync(auth);
        var context = Context(mediator.Object);
        var environment = Mock.Of<IWebHostEnvironment>(e => e.EnvironmentName == "Production");
        var controller = new AuthController(new ConfigurationBuilder().Build(), environment)
            { ControllerContext = new() { HttpContext = context } };
        var response = refresh
            ? await controller.Refresh(new() { RefreshToken = "body" })
            : await controller.Login(new() { Email = "test@example.test", Password = "password" });
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(response).Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("test-access", json.RootElement.GetProperty("accessToken").GetString());
        Assert.False(json.RootElement.TryGetProperty("refreshToken", out _));
        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("ecafe_refresh_token=test-refresh-cookie", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Login")]
    [InlineData("Refresh")]
    [InlineData("Logout")]
    public async Task NativeAuthRejectsInsecureTransportBeforeCallingAuth(string action)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var context = Context(mediator.Object);
        context.Request.Scheme = "http";
        var controller = new MobileAuthController { ControllerContext = new() { HttpContext = context } };
        Task<IActionResult> request = action switch
        {
            "Login" => controller.Login(new() { Email = "e@example.test", Password = "password" }),
            "Refresh" => controller.Refresh(new() { RefreshToken = "test" }),
            _ => controller.Logout(new() { RefreshToken = "test" })
        };
        var error = await Assert.ThrowsAsync<BadRequestException>(() => request);
        Assert.Equal(400, error.StatusCode);
        mediator.VerifyNoOtherCalls();
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Theory]
    [InlineData("/api/v1/mobile/auth/login")]
    [InlineData("/API/V1/MOBILE/AUTH/REFRESH")]
    [InlineData("/api/v1/mobile/auth/logout")]
    public async Task NativeAuthBypassesOnlyExactActiveAccessTokenChecks(string path)
    {
        var context = new DefaultHttpContext { User = new(new ClaimsIdentity([new("userId", "1")], "expired-or-revoked")) };
        context.Request.Path = path;
        var reached = false;
        var middleware = new ActiveUserMiddleware(_ => { reached = true; return Task.CompletedTask; });
        var cache = new Mock<IUserSessionStateCache>(MockBehavior.Strict);
        await middleware.Invoke(context, cache.Object);
        Assert.True(reached);
        cache.VerifyNoOtherCalls();
        context.Request.Path = path + "/other";
        await Assert.ThrowsAsync<UnauthorizedException>(() => middleware.Invoke(context, cache.Object));
    }

    [Fact]
    public void NativeEndpointsKeepLoginRefreshRateLimitsAndNoStoreContract()
    {
        Assert.NotNull(typeof(MobileAuthController).GetCustomAttribute<AllowAnonymousAttribute>());
        var cache = typeof(MobileAuthController).GetCustomAttribute<ResponseCacheAttribute>()!;
        Assert.True(cache.NoStore);
        Assert.Equal(ResponseCacheLocation.None, cache.Location);
        foreach (var action in new[] { "Login", "Refresh", "Logout" })
        {
            var method = typeof(MobileAuthController).GetMethod(action)!;
            Assert.Equal(action == "Login" ? RateLimitPolicyNames.AuthLogin : RateLimitPolicyNames.AuthRefresh,
                method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
            Assert.Null(method.GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(method.GetParameters().Single().GetCustomAttribute<FromBodyAttribute>());
        }
    }

    [Fact]
    public async Task NativeLogoutHandlerUsesSessionRevocationRatherThanWebSingleTokenLogout()
    {
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        var command = new MobileLogoutCommand { RefreshToken = "test" };
        auth.Setup(x => x.LogoutSessionAsync(command)).Returns(Task.CompletedTask);
        await new MobileLogoutCommandHandler(auth.Object).Handle(command, default);
        auth.Verify(x => x.LogoutSessionAsync(command), Times.Once);
        auth.VerifyNoOtherCalls();
    }

    /// <summary>Controller sınaqlarına məxfi olmayan süni tokenlər və HTTPS sorğu konteksti verir; xarici serverə qoşulmur.</summary>
    private static DefaultHttpContext Context(IMediator mediator)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(mediator).BuildServiceProvider()
        };
        context.Request.Scheme = "https";
        return context;
    }
}
