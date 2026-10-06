using ECafe.Api.Security;
using ECafe.Application.Features.MobileApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

public sealed class MobileAppController(
    IMobilePushInstallationService installations,
    IMobileReleaseArtifactService releaseArtifacts) : BaseController
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
    [HttpGet(ApiRoutes.MobileApp.CustomerAccess)]
    public async Task<IActionResult> GetCustomerAccess()
        => Ok(await Mediator.Send(new GetCustomerMobileAccessQuery()));

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.MobileRelease)]
    [HttpGet(ApiRoutes.MobileApp.CustomerRelease)]
    public async Task<IActionResult> GetCustomerRelease(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await Mediator.Send(new GetCustomerMobileReleaseQuery(), cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.MobileRelease)]
    [HttpGet(ApiRoutes.MobileApp.CustomerDownload)]
    public async Task<IActionResult> DownloadCustomerRelease(CancellationToken cancellationToken)
    {
        await Mediator.Send(new GetCustomerMobileAccessQuery(), cancellationToken);
        var release = await releaseArtifacts.OpenVerifiedAsync(cancellationToken);
        if (release is null)
            return NotFound();

        return StreamPrivateRelease(release);
    }

    [Authorize]
    [HttpGet(ApiRoutes.MobileApp.StaffRestaurantAccess)]
    public async Task<IActionResult> GetStaffAccess(int restaurantId)
        => Ok(await Mediator.Send(new GetStaffMobileAccessQuery(restaurantId)));

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.MobileRelease)]
    [HttpGet(ApiRoutes.MobileApp.StaffRestaurantRelease)]
    public async Task<IActionResult> GetStaffRelease(int restaurantId)
        => Ok(await Mediator.Send(new GetStaffMobileReleaseQuery(restaurantId)));

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.MobileRelease)]
    [HttpGet(ApiRoutes.MobileApp.StaffRestaurantDownload)]
    public async Task<IActionResult> DownloadStaffRelease(int restaurantId, CancellationToken cancellationToken)
    {
        var access = await Mediator.Send(new GetStaffMobileAccessQuery(restaurantId), cancellationToken);
        if (!access.Enabled)
            return NotFound();

        var release = await releaseArtifacts.OpenVerifiedAsync(cancellationToken);
        if (release is null)
            return NotFound();

        return StreamPrivateRelease(release);
    }

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

    private IActionResult StreamPrivateRelease(VerifiedMobileRelease release)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(release.Content, "application/vnd.android.package-archive",
            $"ECafe-{release.Version}.apk", enableRangeProcessing: true);
    }
}
