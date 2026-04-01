using ECafe.Application.Features.Commands.Restaurant;
using ECafe.Application.Features.Commands.User;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
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
    }
}
