using ECafe.Application.Features.Commands.Item;
using ECafe.Application.Features.Queries.Item.GetAll;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class ItemController : BaseController
    {
        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpPost("api/v1/restaurants/{restaurantId}/items")]
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

        [HasPermission(PermissionCode.ViewRestaurantInfo)]
        [HttpGet("api/v1/items/getAll")]
        public async Task<IActionResult> GetAll([FromQuery] GetAllItemsQuery query)
        => Ok(await Mediator.Send(query));
    }

    public sealed class CreateItemFormRequest
    {
        public int CategoryId { get; set; }

        public int StatusId { get; set; } = 5001;

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public decimal BasePrice { get; set; }

        public string? UnavailableReason { get; set; }

        public int SalesCount { get; set; }

        public int? FileId { get; set; }
    }
}
