using ECafe.Application.Features.Commands.Table;
using ECafe.Application.Features.Queries.Table;
using ECafe.Application.DTOs.Table;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class TableController : BaseController
    {
        [HasPermission(Domain.Enums.PermissionCode.ManageTables)]
        [HttpPost("api/v1/restaurants/{restaurantId}/tables")]
        public async Task<IActionResult> CreateTable(int restaurantId, [FromForm] CreateTableRequest request)
        {
            var command = new CreateTableCommand
            {
                RestaurantId = restaurantId,
                TableNo = request.TableNo,
                Name = request.Name,
                Capacity = request.Capacity
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantInfo)]
        [HttpGet("api/v1/restaurants/{restaurantId}/tables")]
        public async Task<IActionResult> GetByRestaurant(int restaurantId)
            => Ok(await Mediator.Send(new GetRestaurantTablesQuery(restaurantId)));

        [HasPermission(Domain.Enums.PermissionCode.ManageTables)]
        [HttpPut("api/v1/restaurants/{restaurantId}/tables/{tableId}")]
        public async Task<IActionResult> UpdateTable(int restaurantId, int tableId, [FromBody] UpdateTableRequest request)
        {
            var command = new UpdateTableCommand
            {
                RestaurantId = restaurantId,
                TableId = tableId,
                TableNo = request.TableNo,
                Name = request.Name,
                Capacity = request.Capacity,
                IsActive = request.IsActive
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageTables)]
        [HttpPatch("api/v1/restaurants/{restaurantId}/tables/{tableId}/deactivate")]
        public async Task<IActionResult> DeactivateTable(int restaurantId, int tableId)
            => Ok(await Mediator.Send(new DeactivateTableCommand(restaurantId, tableId)));

        [HasPermission(Domain.Enums.PermissionCode.ManageTables)]
        [HttpDelete("api/v1/restaurants/{restaurantId}/tables/{tableId}")]
        public async Task<IActionResult> DeleteTable(int restaurantId, int tableId)
            => Ok(await Mediator.Send(new DeleteTableCommand(restaurantId, tableId)));
    }
}
