using ECafe.Application.Features.Commands.Restaurant;
using ECafe.Application.Features.Queries.Geocoding;
using ECafe.Application.Features.Queries.Restaurant.GetAll;
using ECafe.Application.Features.Queries.Restaurant.GetById;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{

    public class RestaurantController : BaseController
    {
        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurants)]
        [HttpPost(ApiRoutes.Restaurant.RegisterRestaurant)]
        public async Task<IActionResult> RegisterRestaurant([FromForm] RegisterRestaurantCommand command)
        => Ok(await Mediator.Send(command));

        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurants)]
        [HttpPut(ApiRoutes.Restaurant.UpdateRestaurant)]
        public async Task<IActionResult> UpdateRestaurant(int id, [FromBody] UpdateRestaurantCommand command)
        {
            command.RestaurantId = id;
            await Mediator.Send(command);
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurants)]
        [HttpPatch(ApiRoutes.Restaurant.DeactivateRestaurant)]
        public async Task<IActionResult> DeactivateRestaurant(int id)
        {
            await Mediator.Send(new DeactivateRestaurantCommand { RestaurantId = id });
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantInfo)]
        [HttpGet(ApiRoutes.Restaurant.GetAllRestaurants)]
        public async Task<IActionResult> GetAllRestaurants([FromQuery] GetAllRestaurantsQuery query)
        {
            return Ok(await Mediator.Send(query));
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantInfo)]
        [HttpGet(ApiRoutes.Restaurant.GetByIdRestaurant)]
        public async Task<IActionResult> GetByIdRestaurant(int id)
        => Ok(await Mediator.Send(new GetRestaurantQuery(id)));

        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurants)]
        [HttpGet(ApiRoutes.Restaurant.GeocodeAddress)]
        public async Task<IActionResult> GeocodeAddress([FromQuery] GeocodeAddressQuery query)
        => Ok(await Mediator.Send(query));

    }
}