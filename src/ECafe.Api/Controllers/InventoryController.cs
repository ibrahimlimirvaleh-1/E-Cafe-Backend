using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.DTOs.InventoryMovement;
using ECafe.Application.Features.Commands.InventoryItem.Activate;
using ECafe.Application.Features.Commands.InventoryItem.Create;
using ECafe.Application.Features.Commands.InventoryItem.Deactivate;
using ECafe.Application.Features.Commands.InventoryItem.Delete;
using ECafe.Application.Features.Commands.InventoryItem.Update;
using ECafe.Application.Features.Commands.InventoryMovement.Create;
using ECafe.Application.Features.Queries.InventoryItem.GetAll;
using ECafe.Application.Features.Queries.InventoryItem.GetById;
using ECafe.Application.Features.Queries.InventoryMovement.History;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    [RequireActiveRestaurantContract]
    public class InventoryController : BaseController
    {
        [HasPermission(PermissionCode.ViewInventory)]
        [HttpGet(ApiRoutes.Inventory.GetAll)]
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
        [HttpPost(ApiRoutes.Inventory.Create)]
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

        [HasPermission(PermissionCode.ViewInventory)]
        [HttpGet(ApiRoutes.Inventory.GetById)]
        public async Task<IActionResult> GetById(
            [FromRoute] int restaurantId,
            [FromRoute] int inventoryItemId)
        {
            var query = new GetInventoryItemByIdQuery
            {
                RestaurantId = restaurantId,
                InventoryItemId = inventoryItemId
            };

            return Ok(await Mediator.Send(query));
        }

        [HasPermission(PermissionCode.ManageInventory)]
        [HttpPut(ApiRoutes.Inventory.Update)]
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

        [HasPermission(PermissionCode.ManageInventory)]
        [HttpPatch(ApiRoutes.Inventory.Activate)]
        public async Task<IActionResult> Activate(
            [FromRoute] int restaurantId,
            [FromRoute] int inventoryItemId)
        {
            var command = new ActivateInventoryItemCommand
            {
                RestaurantId = restaurantId,
                InventoryItemId = inventoryItemId
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageInventory)]
        [HttpPatch(ApiRoutes.Inventory.Deactivate)]
        public async Task<IActionResult> Deactivate(
            [FromRoute] int restaurantId,
            [FromRoute] int inventoryItemId)
        {
            var command = new DeactivateInventoryItemCommand
            {
                RestaurantId = restaurantId,
                InventoryItemId = inventoryItemId
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageInventory)]
        [HttpDelete(ApiRoutes.Inventory.Delete)]
        public async Task<IActionResult> Delete(
            [FromRoute] int restaurantId,
            [FromRoute] int inventoryItemId)
        {
            var command = new DeleteInventoryItemCommand
            {
                RestaurantId = restaurantId,
                InventoryItemId = inventoryItemId
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageInventory)]
        [HttpPost(ApiRoutes.Inventory.CreateMovement)]
        public async Task<IActionResult> CreateMovement(
            [FromRoute] int restaurantId,
            [FromRoute] int inventoryItemId,
            [FromBody] CreateInventoryMovementRequest request)
        {
            var command = new CreateInventoryMovementCommand
            {
                RestaurantId = restaurantId,
                InventoryItemId = inventoryItemId,
                Quantity = request.Quantity,
                UnitId = request.UnitId,
                MovementTypeId = request.MovementTypeId,
                Reason = request.Reason
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ViewInventory)]
        [HttpGet(ApiRoutes.Inventory.GetMovementHistory)]
        public async Task<IActionResult> GetMovementHistory(
            [FromRoute] int restaurantId,
            [FromRoute] int inventoryItemId,
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize)
        {
            var query = new GetInventoryMovementHistoryQuery
            {
                RestaurantId = restaurantId,
                InventoryItemId = inventoryItemId,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Ok(await Mediator.Send(query));
        }
    }
}