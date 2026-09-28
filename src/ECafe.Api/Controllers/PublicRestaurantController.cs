using ECafe.Api.Security;
using ECafe.Application.Features.Queries.Restaurant.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECafe.Api.Controllers
{
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.PublicRead)]
    public class PublicRestaurantController : BaseController
    {
        [HttpGet(ApiRoutes.PublicRestaurant.GetRestaurants)]
        public async Task<IActionResult> GetRestaurants([FromQuery] GetPublicRestaurantsQuery query)
            => Ok(await Mediator.Send(query));

        [HttpGet(ApiRoutes.PublicRestaurant.GetRestaurant)]
        public async Task<IActionResult> GetRestaurant(int restaurantId)
            => Ok(await Mediator.Send(new GetPublicRestaurantProfileQuery(restaurantId)));

        [HttpGet(ApiRoutes.PublicRestaurant.GetMenu)]
        public async Task<IActionResult> GetMenu(int restaurantId)
            => Ok(await Mediator.Send(new GetPublicRestaurantMenuQuery(restaurantId)));

        [HttpGet(ApiRoutes.PublicRestaurant.GetStaff)]
        public async Task<IActionResult> GetStaff(int restaurantId)
            => Ok(await Mediator.Send(new GetPublicRestaurantStaffQuery(restaurantId)));

        [HttpGet(ApiRoutes.PublicRestaurant.GetTables)]
        public async Task<IActionResult> GetTables(int restaurantId)
            => Ok(await Mediator.Send(new GetPublicRestaurantTablesQuery(restaurantId)));

        [HttpGet(ApiRoutes.PublicRestaurant.CheckTableAvailability)]
        public async Task<IActionResult> CheckTableAvailability(int restaurantId, [FromQuery] DateTimeOffset reservedAt)
            => Ok(await Mediator.Send(new GetPublicRestaurantTableAvailabilityQuery(restaurantId, reservedAt)));

        [HttpGet(ApiRoutes.PublicRestaurant.GetAvailableTables)]
        public async Task<IActionResult> GetAvailableTables(int restaurantId, [FromQuery] DateTimeOffset reservedAt)
            => Ok(await Mediator.Send(new GetPublicRestaurantAvailableTablesQuery(restaurantId, reservedAt)));
    }
}