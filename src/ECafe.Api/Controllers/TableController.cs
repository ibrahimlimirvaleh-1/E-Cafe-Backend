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
    }
}
