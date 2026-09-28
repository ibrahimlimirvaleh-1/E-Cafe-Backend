using ECafe.Application.Features.Commands.Category;
using ECafe.Application.Features.Queries.Category;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{

    [RequireActiveRestaurantContract]
    public class CategoryController : BaseController
    {
        [HasPermission(PermissionCode.ViewRestaurantInfo)]
        [HttpGet(ApiRoutes.Category.GetAll)]
        public async Task<IActionResult> GetAll([FromRoute] int restaurantId)
        => Ok(await Mediator.Send(new GetAllCategoryQuery(restaurantId)));

        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpPost(ApiRoutes.Category.Create)]
        public async Task<IActionResult> Create(int restaurantId, [FromForm] CreateCategoryCommand command)
        {
            command.RestaurantId = restaurantId;
            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpPut(ApiRoutes.Category.Update)]
        public async Task<IActionResult> Update(int restaurantId, int categoryId, [FromBody] UpdateCategoryCommand command)
        {
            command.RestaurantId = restaurantId;
            command.CategoryId = categoryId;
            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpPatch(ApiRoutes.Category.Activate)]
        public async Task<IActionResult> Activate(int restaurantId, int categoryId)
        {
            return Ok(await Mediator.Send(new ActivateCategoryCommand
            {
                RestaurantId = restaurantId,
                CategoryId = categoryId
            }));
        }

        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpPatch(ApiRoutes.Category.Deactivate)]
        public async Task<IActionResult> Deactivate(int restaurantId, int categoryId)
        {
            return Ok(await Mediator.Send(new DeactivateCategoryCommand
            {
                RestaurantId = restaurantId,
                CategoryId = categoryId
            }));
        }

        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpDelete(ApiRoutes.Category.Delete)]
        public async Task<IActionResult> Delete(int restaurantId, int categoryId)
        {
            return Ok(await Mediator.Send(new DeleteCategoryCommand
            {
                RestaurantId = restaurantId,
                CategoryId = categoryId
            }));
        }
    }
}