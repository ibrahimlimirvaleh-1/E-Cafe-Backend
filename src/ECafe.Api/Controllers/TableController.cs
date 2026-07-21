using ECafe.Application.Features.Commands.Table;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{

    public class TableController : BaseController
    {
        [HasPermission(Domain.Enums.PermissionCode.ManageTables)]
        [HttpPost("api/v1/admin/table/create")]
        public async Task<IActionResult> CreateTable([FromForm] CreateTableCommand command)
        => Ok(await Mediator.Send(command));
    }
}
