using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Features.Commands.InventoryItem.Create;
using ECafe.Application.Features.Commands.InventoryItem.Update;
using ECafe.Application.Features.Queries.InventoryItem.GetAll;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class InventoryController : BaseController
    {
        [HasPermission(PermissionCode.ViewInventory)]
        [HttpGet("api/v1/restaurants/{restaurantId}/inventory")]
        public async Task<IActionResult> GetAll([FromRoute] int restaurantId, [FromQuery] GetInventoryItemsRequest request)
        {
            var query = new GetInventoryItemsQuery
            {
                RestaurantId = restaurantId,
                Search = request.Search,
                OnlyLowStock = request.OnlyLowStock,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };

            return Ok(await Mediator.Send(query));
        }

        [HasPermission(PermissionCode.ManageInventory)]
        [HttpPost("api/v1/restaurants/{restaurantId}/inventory")]
        public async Task<IActionResult> Create([FromRoute] int restaurantId, [FromBody] CreateInventoryItemRequest request)
        {
            var command = new CreateInventoryItemCommand
            {
                RestaurantId = restaurantId,
                Name = request.Name,
                UnitId = request.UnitId,
                QuantityOnHand = request.QuantityOnHand,
                LowStockThreshold = request.LowStockThreshold
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageInventory)]
        [HttpPut("api/v1/restaurants/{restaurantId}/inventory/{inventoryItemId}")]
        public async Task<IActionResult> Update(
            [FromRoute] int restaurantId,
            [FromRoute] int inventoryItemId,
            [FromBody] UpdateInventoryItemRequest request)
        {
            var command = new UpdateInventoryItemCommand
            {
                RestaurantId = restaurantId,
                InventoryItemId = inventoryItemId,
                Name = request.Name,
                UnitId = request.UnitId,
                LowStockThreshold = request.LowStockThreshold,
                IsActive = request.IsActive
            };

            return Ok(await Mediator.Send(command));
        }
    }
}
