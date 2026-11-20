using ECafe.Api.Security;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Services.Auth.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class EmailChangeController(IEmailChangeService service) : BaseController
{
    [HttpPost(ApiRoutes.User.RequestEmailChange)]
    [Consumes("application/json")]
    [EnableRateLimiting(RateLimitPolicyNames.EmailChangeRequest)]
    public async Task<IActionResult> RequestChange([FromBody] RequestEmailChangeRequest request, CancellationToken cancellationToken)
    {
        await service.RequestAsync(request, cancellationToken);
        return Accepted();
    }

    [HttpPost(ApiRoutes.User.ConfirmEmailChange)]
    [Consumes("application/json")]
    [EnableRateLimiting(RateLimitPolicyNames.EmailChangeConfirm)]
    public async Task<IActionResult> Confirm([FromBody] ConfirmEmailChangeRequest request, CancellationToken cancellationToken)
    {
        await service.ConfirmAsync(request, cancellationToken);
        Response.Cookies.Delete("ecafe_refresh_token", new CookieOptions
        {
            Path = ApiRoutes.Auth.CookiePath, HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax
        });
        return NoContent();
    }
}
