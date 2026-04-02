using ECafe.Application.Features.Commands.User.Create;
using ECafe.Application.Features.Commands.User.Delete;
using ECafe.Application.Features.Commands.User.UpdateRole;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class UserController : BaseController
    {
        [HasPermission(Domain.Enums.PermissionCode.ManageStaff)]
        [HttpPost("api/user/create")]
        public async Task<IActionResult> Create([FromForm] CreateUserCommand command)
        {
            await Mediator.Send(command);
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageStaff)]
        [HttpDelete("api/user/delete")]
        public async Task<IActionResult> Delete([FromQuery] DeleteUserCommand command)
        {
            await Mediator.Send(command);
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageStaff)]
        [HttpPatch("api/user/role/update")]
        public async Task<IActionResult> UpdateRole([FromQuery] UpdateRoleCommand command)
        {
            await Mediator.Send(command);
            return Ok();
        }
    }
}
