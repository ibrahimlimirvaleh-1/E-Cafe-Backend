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
        [HttpPost("api/restaurant/register")]
        public async Task<IActionResult> RegisterRestaurant([FromForm] RegisterRestaurantCommand command)
        => Ok(await Mediator.Send(command));

        [HttpGet("api/restaurants/getAll")]
        public async Task<IActionResult> GetAllRestaurants()
        {
            var queryResult = new GetAllRestaurantsQuery();
            return Ok(await Mediator.Send(queryResult));
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantInfo)]
        [HttpGet("api/restaurant/getById/{id}")]
        public async Task<IActionResult> GetByIdRestaurant(int id)
        => Ok(await Mediator.Send(new GetRestaurantQuery(id)));

    }
}
