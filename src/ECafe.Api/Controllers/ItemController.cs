using ECafe.Api.Requests.Item;
using ECafe.Application.Features.Commands.Item;
using ECafe.Application.Features.Queries.Item.GetAll;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    [RequireActiveRestaurantContract]
    public class ItemController : BaseController
    {
        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpPost(ApiRoutes.Item.Create)]
        public async Task<IActionResult> Create(int restaurantId, [FromForm] CreateItemFormRequest request)
        {
            var command = new CreateItemCommand
            {
                RestaurantId = restaurantId,
                CategoryId = request.CategoryId,
                StatusId = request.StatusId,
                Name = request.Name,
                Description = request.Description,
                BasePrice = request.BasePrice,
                UnavailableReason = request.UnavailableReason,
                SalesCount = request.SalesCount,
                FileId = request.FileId
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpPut(ApiRoutes.Item.Update)]
        public async Task<IActionResult> Update(int restaurantId, int itemId, [FromForm] UpdateItemFormRequest request)
        {
            var command = new UpdateItemCommand
            {
                RestaurantId = restaurantId,
                ItemId = itemId,
                CategoryId = request.CategoryId,
                StatusId = request.StatusId,
                Name = request.Name,
                Description = request.Description,
                BasePrice = request.BasePrice,
                UnavailableReason = request.UnavailableReason,
                SalesCount = request.SalesCount,
                FileId = request.FileId
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpPatch(ApiRoutes.Item.Deactivate)]
        public async Task<IActionResult> Deactivate(int restaurantId, int itemId)
        {
            return Ok(await Mediator.Send(new DeactivateItemCommand
            {
                RestaurantId = restaurantId,
                ItemId = itemId
            }));
        }

        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpDelete(ApiRoutes.Item.Delete)]
        public async Task<IActionResult> Delete(int restaurantId, int itemId)
        {
            return Ok(await Mediator.Send(new DeleteItemCommand
            {
                RestaurantId = restaurantId,
                ItemId = itemId
            }));
        }

        [HasPermission(PermissionCode.ViewRestaurantInfo)]
        [HttpGet(ApiRoutes.Item.GetAll)]
        public async Task<IActionResult> GetAll([FromQuery] GetAllItemsQuery query)
        => Ok(await Mediator.Send(query));
    }
}