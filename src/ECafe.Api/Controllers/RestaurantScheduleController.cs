using ECafe.Application.Services.Restaurant.Schedule;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class RestaurantScheduleController(IRestaurantScheduleService service) : BaseController
{
    [HasPermission(PermissionCode.EditRestaurantInfo)]
    [HttpGet(ApiRoutes.RestaurantSchedule.Get)]
    public async Task<IActionResult> Get(int restaurantId, CancellationToken cancellationToken)
        => Ok(await service.GetAsync(restaurantId, cancellationToken));

    [Authorize(Roles = "5")]
    [HttpGet(ApiRoutes.RestaurantSchedule.CustomerOffer)]
    public async Task<IActionResult> CustomerOffer(int reservationId, CancellationToken cancellationToken)
        => Ok(await service.GetCustomerOfferAsync(reservationId, cancellationToken));
}
