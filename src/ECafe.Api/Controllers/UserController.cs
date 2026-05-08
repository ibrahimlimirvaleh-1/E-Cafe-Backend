using ECafe.Application.Features.Commands.User.Create;
using ECafe.Application.Features.Commands.User.Delete;
using ECafe.Application.Features.Commands.User.UpdateRole;
using ECafe.Application.Features.Queries.Restaurant.GetAll;
using ECafe.Infrastructure.Authorization;
using ECafe.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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


        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantInfo)]
        [HttpGet("api/staff/{restaurantId}")]
        public async Task<IActionResult> GetStaff(int restaurantId)
        {
            var role = ClaimsPrincipalExtensions.GetRoleId(User);
            var result = await Mediator.Send(new GetRestaurantStaffQuery(restaurantId,role));
            return Ok(result);
        }
    }
}
