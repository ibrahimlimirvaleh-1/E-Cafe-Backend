using ECafe.Application.Features.Commands.RestaurantGroup;
using ECafe.Application.Features.Queries.RestaurantGroup;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers
{
    public class RestaurantGroupController : BaseController
    {
        [HasPermission(PermissionCode.ViewRestaurantInfo)]
        [HttpGet(ApiRoutes.RestaurantGroup.GetAll)]
        public async Task<IActionResult> GetAll()
            => Ok(await Mediator.Send(new GetRestaurantGroupsQuery()));

        [HasPermission(PermissionCode.ManageRestaurants)]
        [HttpPost(ApiRoutes.RestaurantGroup.Create)]
        public async Task<IActionResult> Create([FromBody] CreateRestaurantGroupCommand command)
            => Ok(await Mediator.Send(command));
    }
}
