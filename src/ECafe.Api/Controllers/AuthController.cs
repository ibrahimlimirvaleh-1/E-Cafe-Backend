using ECafe.Application.Features.Commands.Auth.Login;
using ECafe.Application.Features.Commands.Auth.Logout;
using ECafe.Application.Features.Commands.Auth.LogoutAll;
using ECafe.Application.Features.Commands.Auth.ForgotPassword;
using ECafe.Application.Features.Commands.Auth.Refresh;
using ECafe.Application.Features.Commands.Auth.Register;
using ECafe.Application.Features.Commands.Auth.ResetPassword;
using ECafe.Application.Features.Commands.Auth.SetPassword;
using ECafe.Application.DTOs.Auth;
using ECafe.Api.Routes;
using ECafe.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECafe.Api.Controllers
{

    public class AuthController : BaseController
    {
        private const string RefreshTokenCookieName = "ecafe_refresh_token";

        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public AuthController(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        [HttpPost(ApiRoutes.Auth.Login)]
        [Consumes("application/json")]
        [EnableRateLimiting(RateLimitPolicyNames.AuthLogin)]
        public async Task<IActionResult> Login([FromBody] LoginUserCommand command)
        {
            var auth = await Mediator.Send(command);
            SetRefreshTokenCookie(auth.RefreshToken);

            return Ok(ToClientAuthResponse(auth));
        }


        [HttpPost(ApiRoutes.Auth.Register)]
        [EnableRateLimiting(RateLimitPolicyNames.AuthLogin)]
        public async Task<IActionResult> Register([FromForm] RegisterUserCommand command)
        {
            var auth = await Mediator.Send(command);
            SetRefreshTokenCookie(auth.RefreshToken);

            return Ok(ToClientAuthResponse(auth));
        }

        [HttpPost(ApiRoutes.Auth.SetPassword)]
        [Consumes("application/json")]
        [EnableRateLimiting(RateLimitPolicyNames.AuthLogin)]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordCommand command)
        {
            await Mediator.Send(command);
            return Ok(new { message = "Password has been set successfully." });
        }

        [HttpPost(ApiRoutes.Auth.ForgotPassword)]
        [Consumes("application/json")]
        [EnableRateLimiting(RateLimitPolicyNames.AuthPasswordReset)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
        {
            await Mediator.Send(command);
            return Ok(new { message = "If this email exists, a password reset link has been sent." });
        }

        [HttpPost(ApiRoutes.Auth.ResetPassword)]
        [Consumes("application/json")]
        [EnableRateLimiting(RateLimitPolicyNames.AuthPasswordReset)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
        {
            await Mediator.Send(command);
            return Ok(new { message = "Password has been reset successfully." });
        }



        [HttpPost(ApiRoutes.Auth.Refresh)]
        [EnableRateLimiting(RateLimitPolicyNames.AuthRefresh)]
        public async Task<IActionResult> Refresh([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshTokenCommand? command)
        {
            command ??= new RefreshTokenCommand();
            command.RefreshToken = ResolveRefreshToken(command.RefreshToken);

            var auth = await Mediator.Send(command);
            SetRefreshTokenCookie(auth.RefreshToken);

            return Ok(ToClientAuthResponse(auth));
        }

        [HttpPost(ApiRoutes.Auth.Logout)]
        [EnableRateLimiting(RateLimitPolicyNames.AuthRefresh)]
        public async Task<IActionResult> Logout([FromBody] LogoutCommand? command)
        {
            command ??= new LogoutCommand();
            command.RefreshToken = ResolveRefreshToken(command.RefreshToken);

            ClearRefreshTokenCookie();

            if (!string.IsNullOrWhiteSpace(command.RefreshToken))
                await Mediator.Send(command);

            return NoContent();
        }

        [Authorize]
        [HttpPost(ApiRoutes.Auth.LogoutAll)]
        [EnableRateLimiting(RateLimitPolicyNames.AuthRefresh)]
        public async Task<IActionResult> LogoutAll()
        {
            ClearRefreshTokenCookie();
            await Mediator.Send(new LogoutAllCommand());

            return NoContent();
        }

        private object ToClientAuthResponse(AuthResponseDto auth)
            => new
            {
                auth.AccessToken
            };

        private string ResolveRefreshToken(string? requestRefreshToken)
        {
            if (!string.IsNullOrWhiteSpace(requestRefreshToken))
                return requestRefreshToken;

            return Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieRefreshToken)
                ? cookieRefreshToken
                : string.Empty;
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            Response.Cookies.Append(RefreshTokenCookieName, refreshToken, CreateRefreshTokenCookieOptions());
        }

        private void ClearRefreshTokenCookie()
        {
            Response.Cookies.Delete(RefreshTokenCookieName, CreateRefreshTokenCookieDeleteOptions());
        }

        private CookieOptions CreateRefreshTokenCookieOptions()
        {
            var lifetimeDays = _configuration.GetValue("Authentication:RefreshTokenCookieDays", 30);

            return new CookieOptions
            {
                HttpOnly = true,
                Secure = ShouldUseSecureCookie(),
                SameSite = SameSiteMode.Lax,
                Path = ApiRoutes.UserCookiePath,
                Expires = DateTimeOffset.UtcNow.AddDays(lifetimeDays)
            };
        }

        private CookieOptions CreateRefreshTokenCookieDeleteOptions()
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = ShouldUseSecureCookie(),
                SameSite = SameSiteMode.Lax,
                Path = ApiRoutes.UserCookiePath
            };
        }

        private bool ShouldUseSecureCookie()
        {
            if (_environment.IsDevelopment() || _environment.IsEnvironment("Local"))
                return Request.IsHttps;

            return true;
        }

    }
}