using ECafe.Api.Security;
using ECafe.Application.DTOs.ProductAnnouncements;
using ECafe.Application.Services.ProductAnnouncements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[EnableRateLimiting(RateLimitPolicyNames.ProductAnnouncements)]
public sealed class ProductAnnouncementController(IProductAnnouncementService service) : BaseController
{
    [HttpGet(ApiRoutes.ProductAnnouncements.Pending)]
    public async Task<IActionResult> Pending(CancellationToken cancellationToken)
        => Ok(await service.PendingAsync(cancellationToken));

    [HttpPost(ApiRoutes.ProductAnnouncements.Acknowledge)]
    [Consumes("application/json")]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> Acknowledge([FromRoute] int id, [FromBody] AnnouncementAcknowledgeRequest request, CancellationToken cancellationToken)
    {
        await service.AcknowledgeAsync(id, request, cancellationToken);
        return NoContent();
    }
}
