using ECafe.Application.Features.Commands;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    
    public class RestaurantController : BaseController
    {
        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurants)]
        [HttpPost("api/restaurant/register")]
        public async Task<IActionResult> RegisterRestaurant([FromBody] RegisterRestaurantCommand command)
        => Ok(await Mediator.Send(command));
    }
}
