using ECafe.Application.Features.Commands.Restaurant;
using ECafe.Application.Features.Queries.Restaurant.GetAll;
using ECafe.Application.Features.Queries.Restaurant.GetById;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{

    public class RestaurantController : BaseController
    {
        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurants)]
        [HttpPost("api/v1/admin/restaurants")]
        public async Task<IActionResult> RegisterRestaurant([FromForm] RegisterRestaurantCommand command)
        => Ok(await Mediator.Send(command));

        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurants)]
        [HttpPut("api/v1/admin/restaurants/{id}")]
        public async Task<IActionResult> UpdateRestaurant(int id, [FromBody] UpdateRestaurantCommand command)
        {
            command.RestaurantId = id;
            await Mediator.Send(command);
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurants)]
        [HttpPatch("api/v1/admin/restaurants/{id}/deactivate")]
        public async Task<IActionResult> DeactivateRestaurant(int id)
        {
            await Mediator.Send(new DeactivateRestaurantCommand { RestaurantId = id });
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantInfo)]
        [HttpGet("api/v1/restaurants/getAll")]
        public async Task<IActionResult> GetAllRestaurants([FromQuery] GetAllRestaurantsQuery query)
        {
            return Ok(await Mediator.Send(query));
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantInfo)]
        [HttpGet("api/v1/restaurant/getById/{id}")]
        public async Task<IActionResult> GetByIdRestaurant(int id)
        => Ok(await Mediator.Send(new GetRestaurantQuery(id)));

    }
}
