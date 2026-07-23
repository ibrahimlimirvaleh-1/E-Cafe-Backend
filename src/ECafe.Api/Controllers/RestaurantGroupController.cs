using ECafe.Application.Features.Commands.RestaurantGroup;
using ECafe.Application.Features.Queries.RestaurantGroup;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class RestaurantGroupController : BaseController
    {
        [HasPermission(PermissionCode.ViewRestaurantInfo)]
        [HttpGet("api/v1/restaurant-groups")]
        public async Task<IActionResult> GetAll()
            => Ok(await Mediator.Send(new GetRestaurantGroupsQuery()));

        [HasPermission(PermissionCode.ManageRestaurants)]
        [HttpPost("api/v1/restaurant-groups")]
        public async Task<IActionResult> Create([FromBody] CreateRestaurantGroupCommand command)
            => Ok(await Mediator.Send(command));
    }
}
