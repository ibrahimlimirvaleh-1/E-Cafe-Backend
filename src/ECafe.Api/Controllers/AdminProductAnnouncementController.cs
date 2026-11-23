using ECafe.Api.Security;
using ECafe.Application.DTOs.ProductAnnouncements;
using ECafe.Application.Services.ProductAnnouncements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize(Roles = "1")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[EnableRateLimiting(RateLimitPolicyNames.ProductAnnouncements)]
[RequestSizeLimit(16 * 1024)]
public sealed class AdminProductAnnouncementController(IProductAnnouncementService service) : BaseController
{
    [HttpGet(ApiRoutes.ProductAnnouncements.AdminList)]
    public async Task<IActionResult> List([FromQuery] int? beforeId, CancellationToken cancellationToken)
        => Ok(await service.AdminPageAsync(beforeId, cancellationToken));

    [HttpGet(ApiRoutes.ProductAnnouncements.Destinations)]
    public async Task<IActionResult> Destinations(CancellationToken cancellationToken)
        => Ok((await service.DestinationsAsync(cancellationToken)).Select(value => new { value.Key, value.RoleIds }));

    [HttpPost(ApiRoutes.ProductAnnouncements.AdminList)]
    [Consumes("application/json")]
    public async Task<IActionResult> Create([FromBody] AnnouncementWriteRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateAsync(request, cancellationToken));

    [HttpPut(ApiRoutes.ProductAnnouncements.AdminItem)]
    [Consumes("application/json")]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] AnnouncementWriteRequest request, CancellationToken cancellationToken)
        => Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpPost(ApiRoutes.ProductAnnouncements.Publish)]
    [Consumes("application/json")]
    public async Task<IActionResult> Publish([FromRoute] int id, [FromBody] AnnouncementVersionRequest request, CancellationToken cancellationToken)
        => Ok(await service.SetPublishedAsync(id, request, true, cancellationToken));

    [HttpPost(ApiRoutes.ProductAnnouncements.Unpublish)]
    [Consumes("application/json")]
    public async Task<IActionResult> Unpublish([FromRoute] int id, [FromBody] AnnouncementVersionRequest request, CancellationToken cancellationToken)
        => Ok(await service.SetPublishedAsync(id, request, false, cancellationToken));
}
