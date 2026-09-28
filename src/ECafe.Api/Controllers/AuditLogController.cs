using ECafe.Application.Features.Queries.AuditLog;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class AuditLogController : BaseController
    {
        [HasPermission(Domain.Enums.PermissionCode.ViewAuditLogs)]
        [HttpGet(ApiRoutes.AuditLog.GetRestaurantTimeline)]
        public async Task<IActionResult> GetRestaurantTimeline(
            int restaurantId,
            [FromQuery] GetRestaurantAuditLogsQuery query)
        {
            query.RestaurantId = restaurantId;
            return Ok(await Mediator.Send(query));
        }
    }
}