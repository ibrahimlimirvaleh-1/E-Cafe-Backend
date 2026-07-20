using ECafe.Application.Features.Commands.User.Create;
using ECafe.Application.Features.Commands.User.Delete;
using ECafe.Application.Features.Commands.User.UpdateProfile;
using ECafe.Application.Features.Commands.User.UpdateRole;
using ECafe.Application.Features.Queries.Restaurant.GetAll;
using ECafe.Application.Features.Queries.User.GetAll;
using ECafe.Application.Features.Queries.User.GetProfile;
using ECafe.Application.Features.Queries.User.GetStaffDetail;
using ECafe.Infrastructure.Authorization;
using ECafe.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{

    public class UserController : BaseController
    {
        [HasPermission(Domain.Enums.PermissionCode.ManageStaff)]
        [HttpPost("api/v1/user/create")]
        public async Task<IActionResult> Create([FromForm] CreateUserCommand command)
        {
            await Mediator.Send(command);
            return Ok();
        }


        [HasPermission(Domain.Enums.PermissionCode.ManageStaff)]
        [HttpDelete("api/v1/user/delete")]
        public async Task<IActionResult> Delete([FromQuery] DeleteUserCommand command)
        {
            await Mediator.Send(command);
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageStaff)]
        [HttpPatch("api/v1/user/role/update")]
        public async Task<IActionResult> UpdateRole([FromQuery] UpdateRoleCommand command)
        {
            await Mediator.Send(command);
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageUsers)]
        [HttpGet("api/v1/users")]
        public async Task<IActionResult> GetAll([FromQuery] GetAllUsersQuery query)
        {
            var result = await Mediator.Send(query);
            return Ok(result);
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantInfo)]
        [RequireActiveRestaurantContract]
        [HttpGet("api/v1/staff/{restaurantId}")]
        public async Task<IActionResult> GetStaff(int restaurantId)
        {
            var role = User.GetRoleId();
            var result = await Mediator.Send(new GetRestaurantStaffQuery(restaurantId, role));
            return Ok(result);
        }

        [Authorize]
        [HttpGet("api/v1/profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.GetUserId();
            var result = await Mediator.Send(new GetProfileQuery(userId));
            return Ok(result);
        }

        [Authorize]
        [HttpPut("api/v1/profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileCommand command)
        {
            command.UserId = User.GetUserId();
            await Mediator.Send(command);
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantInfo)]
        [RequireActiveRestaurantContract]
        [HttpGet("api/v1/staff/{restaurantId}/detail/{staffId}")]

        public async Task<IActionResult> GetStaffDetail(int restaurantId, int staffId)
        {
            var result = await Mediator.Send(new GetStaffDetailQuery(restaurantId, staffId));
            return Ok(result);
        }
    }
}
