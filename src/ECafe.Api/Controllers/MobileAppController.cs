using ECafe.Api.Security;
using ECafe.Application.Features.MobileApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

public sealed class MobileAppController(IMobilePushInstallationService installations) : BaseController
{
    [Authorize(Roles = "1")]
    [HttpGet(ApiRoutes.MobileApp.RestaurantModule)]
    public async Task<IActionResult> GetRestaurantModule(int restaurantId)
        => Ok(await Mediator.Send(new GetMobileModuleQuery(restaurantId)));

    [Authorize(Roles = "1")]
    [HttpPut(ApiRoutes.MobileApp.RestaurantModule)]
    public async Task<IActionResult> UpdateRestaurantModule(int restaurantId, [FromBody] UpdateMobileModuleRequest request)
        => Ok(await Mediator.Send(new UpdateMobileModuleCommand(
            restaurantId, request.MobilePushEnabled, request.ShowDownloadLink)));

    [Authorize(Roles = "1")]
    [HttpGet(ApiRoutes.MobileApp.Publication)]
    public async Task<IActionResult> GetPublication()
        => Ok(await Mediator.Send(new GetMobilePublicationQuery()));

    [Authorize(Roles = "1")]
    [HttpPut(ApiRoutes.MobileApp.Publication)]
    public async Task<IActionResult> UpdatePublication([FromBody] UpdateMobilePublicationRequest request)
        => Ok(await Mediator.Send(new UpdateMobilePublicationCommand(request.PublicDownloadEnabled)));

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.PublicRead)]
    [HttpGet(ApiRoutes.MobileApp.PublicRelease)]
    public async Task<IActionResult> GetPublicRelease()
        => Ok(await Mediator.Send(new GetPublicMobileReleaseQuery(null)));

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.PublicRead)]
    [HttpGet(ApiRoutes.MobileApp.PublicRestaurantRelease)]
    public async Task<IActionResult> GetPublicRestaurantRelease(int restaurantId)
        => Ok(await Mediator.Send(new GetPublicMobileReleaseQuery(restaurantId)));

    [Authorize]
    [HttpPut(ApiRoutes.MobileApp.PushInstallation)]
    public async Task<IActionResult> RegisterInstallation(
        Guid installationId,
        [FromBody] RegisterMobilePushInstallationRequest request,
        CancellationToken cancellationToken)
        => Ok(await installations.RegisterAsync(installationId, request, cancellationToken));

    [Authorize]
    [HttpDelete(ApiRoutes.MobileApp.PushInstallation)]
    public async Task<IActionResult> DeactivateInstallation(Guid installationId, CancellationToken cancellationToken)
    {
        await installations.DeactivateAsync(installationId, cancellationToken);
        return NoContent();
    }
}
