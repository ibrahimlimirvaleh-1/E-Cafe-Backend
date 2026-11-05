using ECafe.Api.Security;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Features.Commands.Auth.Login;
using ECafe.Application.Features.Commands.Auth.MobileLogout;
using ECafe.Application.Features.Commands.Auth.Refresh;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[AllowAnonymous]
[Consumes("application/json")]
[RequestSizeLimit(16_384)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MobileAuthController : BaseController
{
    /// <summary>Mobil girişdə mövcud parol və rol yoxlamalarını çağırır; hər iki tokeni yalnız HTTPS JSON cavabında verir.</summary>
    [HttpPost(ApiRoutes.MobileAuth.Login)]
    [EnableRateLimiting(RateLimitPolicyNames.AuthLogin)]
    public async Task<IActionResult> Login([FromBody] LoginUserCommand command)
    {
        EnsureSecureRequest();
        if (command is null || string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
            throw new BadRequestException(ErrorCode.BadRequest);

        return Ok(await Mediator.Send(command));
    }

    /// <summary>Yalnız body-dəki refresh tokeni rotasiya edir; browser kukisi oxumur və yazmır.</summary>
    [HttpPost(ApiRoutes.MobileAuth.Refresh)]
    [EnableRateLimiting(RateLimitPolicyNames.AuthRefresh)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command)
    {
        EnsureSecureRequest();
        return Ok(await Mediator.Send(command));
    }

    /// <summary>Access token tələb etmədən yalnız body tokeninin mobil sessiya ailəsini idempotent bağlayır.</summary>
    [HttpPost(ApiRoutes.MobileAuth.Logout)]
    [EnableRateLimiting(RateLimitPolicyNames.AuthRefresh)]
    public async Task<IActionResult> Logout([FromBody] MobileLogoutCommand command)
    {
        EnsureSecureRequest();
        await Mediator.Send(command);
        return NoContent();
    }

    /// <summary>Native tokenlərin HTTP cavabında verilməsini rədd edir; 400 cavabı etimadnamənin ləğvi sayılmır.</summary>
    private void EnsureSecureRequest()
    {
        if (!Request.IsHttps)
            throw new BadRequestException(ErrorCode.BadRequest);
    }
}
