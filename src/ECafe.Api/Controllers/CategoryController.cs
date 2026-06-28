using ECafe.Application.Features.Commands.Category;
using ECafe.Application.Features.Queries.Category;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{

    public class CategoryController : BaseController
    {
        [HttpGet("api/v1/category/{restaurantId}")]
        public async Task<IActionResult> GetAll([FromRoute] int restaurantId)
        => Ok(await Mediator.Send(new GetAllCategoryQuery(restaurantId)));

        [HasPermission(PermissionCode.ManageCatalog)]
        [HttpPost("api/v1/category")]
        public async Task<IActionResult> Create([FromForm] CreateCategoryCommand command)
        => Ok(await Mediator.Send(command));
    }
}
