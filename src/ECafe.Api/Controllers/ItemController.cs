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
        [HttpPost("api/v1/item")]
        public async Task<IActionResult> Create([FromForm] CreateItemCommand command)
        => Ok(await Mediator.Send(command));

        [HttpGet("api/v1/items/getAll")]
        public async Task<IActionResult> GetAll([FromQuery] GetAllItemsQuery query)
        => Ok(await Mediator.Send(query));
    }
}
