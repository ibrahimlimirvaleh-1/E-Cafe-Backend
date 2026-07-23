using ECafe.Application.Features.Commands.Category;
using ECafe.Application.Features.Queries.Category;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{

    public class CategoryController : BaseController
    {
        [HasPermission(PermissionCode.ViewRestaurantInfo)]
        [HttpGet("api/v1/category/{restaurantId}")]
        public async Task<IActionResult> GetAll([FromRoute] int restaurantId)
        => Ok(await Mediator.Send(new GetAllCategoryQuery(restaurantId)));

        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpPost("api/v1/restaurants/{restaurantId}/categories")]
        public async Task<IActionResult> Create(int restaurantId, [FromForm] CreateCategoryCommand command)
        {
            command.RestaurantId = restaurantId;
            return Ok(await Mediator.Send(command));
        }
    }
}
