using ECafe.Api.Security;
using System.Net.Mime;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Services.Auth.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize(Roles = "5")]
public sealed class CustomerAccountDeletionController(ICustomerAccountDeletionService service) : BaseController
{
    [HttpGet(ApiRoutes.User.AccountDeletion)]
    [EnableRateLimiting(RateLimitPolicyNames.PublicRead)]
    public async Task<IActionResult> GetEligibility(CancellationToken cancellationToken)
        => Ok(await service.GetEligibilityAsync(cancellationToken));

    [HttpPost(ApiRoutes.User.AccountDeletion)]
    [Consumes(MediaTypeNames.Application.Json)]
    [EnableRateLimiting(RateLimitPolicyNames.CustomerAccountDeletion)]
    public async Task<IActionResult> Delete([FromBody] CustomerAccountDeletionRequest request, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(request, cancellationToken);
        Response.Cookies.Delete("ecafe_refresh_token", new CookieOptions
        { Path = ApiRoutes.Auth.CookiePath, HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax });
        return NoContent();
    }
}
