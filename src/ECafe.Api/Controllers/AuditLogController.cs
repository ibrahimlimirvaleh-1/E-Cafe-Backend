using ECafe.Application.Features.Queries.AuditLog;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class AuditLogController : BaseController
    {
        [HasPermission(Domain.Enums.PermissionCode.ViewAuditLogs)]
        [HttpGet("api/v1/restaurants/{restaurantId}/audit-logs")]
        public async Task<IActionResult> GetRestaurantTimeline(
            int restaurantId,
            [FromQuery] GetRestaurantAuditLogsQuery query)
        {
            query.RestaurantId = restaurantId;
            return Ok(await Mediator.Send(query));
        }
    }
}
