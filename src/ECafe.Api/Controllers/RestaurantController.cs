using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.Features.Commands.Restaurant;
using ECafe.Application.Features.Queries.Geocoding;
using ECafe.Application.Features.Queries.Restaurant.GetAll;
using ECafe.Application.Features.Queries.Restaurant.GetById;
using ECafe.Infrastructure.Authorization;
using ECafe.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers
{

    public class RestaurantController : BaseController
    {
        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurants)]
        [HttpPost(ApiRoutes.Restaurant.RegisterRestaurant)]
        public async Task<IActionResult> RegisterRestaurant([FromForm] RegisterRestaurantCommand command)
        => Ok(await Mediator.Send(command));

        [HasPermission(PermissionCode.EditRestaurantInfo)]
        [HttpPut(ApiRoutes.Restaurant.UpdateRestaurant)]
        public async Task<IActionResult> UpdateRestaurant(int restaurantId, [FromBody] UpdateRestaurantCommand command)
        {
            command.RestaurantId = restaurantId;
            await Mediator.Send(command);
            return Ok();
        }

        [HasPermission(PermissionCode.EditRestaurantInfo)]
        [HttpPut(ApiRoutes.Restaurant.SetDepositRule)]
        public async Task<IActionResult> SetDepositRule(int restaurantId, DateOnly reservationDate, [FromBody] SetRestaurantDepositRuleRequest request)
        {
            await Mediator.Send(new SetRestaurantDepositRuleCommand(restaurantId, reservationDate, request.Amount));
            return NoContent();
        }

        [HasPermission(PermissionCode.EditRestaurantInfo)]
        [HttpDelete(ApiRoutes.Restaurant.RemoveDepositRule)]
        public async Task<IActionResult> RemoveDepositRule(int restaurantId, DateOnly reservationDate)
        {
            await Mediator.Send(new RemoveRestaurantDepositRuleCommand(restaurantId, reservationDate));
            return NoContent();
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

        [HasPermission(PermissionCode.EditRestaurantInfo)]
        [HttpGet(ApiRoutes.Restaurant.GeocodeAddress)]
        public async Task<IActionResult> GeocodeAddress([FromQuery] GeocodeAddressQuery query)
        => Ok(await Mediator.Send(query));

    }
}
