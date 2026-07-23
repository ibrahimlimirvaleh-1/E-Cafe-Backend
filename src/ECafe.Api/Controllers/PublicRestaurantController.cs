using ECafe.Application.Features.Queries.Restaurant.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    [AllowAnonymous]
    public class PublicRestaurantController : BaseController
    {
        [HttpGet("api/v1/public/restaurants")]
        public async Task<IActionResult> GetRestaurants([FromQuery] GetPublicRestaurantsQuery query)
            => Ok(await Mediator.Send(query));

        [HttpGet("api/v1/public/restaurants/{restaurantId}")]
        public async Task<IActionResult> GetRestaurant(int restaurantId)
            => Ok(await Mediator.Send(new GetPublicRestaurantProfileQuery(restaurantId)));

        [HttpGet("api/v1/public/restaurants/{restaurantId}/menu")]
        public async Task<IActionResult> GetMenu(int restaurantId)
            => Ok(await Mediator.Send(new GetPublicRestaurantMenuQuery(restaurantId)));

        [HttpGet("api/v1/public/restaurants/{restaurantId}/staff")]
        public async Task<IActionResult> GetStaff(int restaurantId)
            => Ok(await Mediator.Send(new GetPublicRestaurantStaffQuery(restaurantId)));

        [HttpGet("api/v1/public/restaurants/{restaurantId}/tables")]
        public async Task<IActionResult> GetTables(int restaurantId)
            => Ok(await Mediator.Send(new GetPublicRestaurantTablesQuery(restaurantId)));
    }
}
