using ECafe.Application.Features.Commands.User.ActivateStaff;
using ECafe.Application.Features.Commands.User.Create;
using ECafe.Application.Features.Commands.User.DeactivateStaff;
using ECafe.Application.Features.Commands.User.Delete;
using ECafe.Application.Features.Commands.User.UpdateProfile;
using ECafe.Application.Features.Commands.User.UpdateRole;
using ECafe.Application.Features.Commands.User.UpdateStaff;
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
        [HttpPost("api/v1/users")]
        public async Task<IActionResult> Create([FromForm] CreateUserCommand command)
        {
            await Mediator.Send(command);
            return Ok();
        }



        [HasPermission(Domain.Enums.PermissionCode.ManageStaff)]
        [HttpDelete("api/v1/users/{userId:int}")]
        public async Task<IActionResult> Delete(int userId)
        {
            var command = new DeleteUserCommand { Id = userId };
            await Mediator.Send(command);
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageStaff)]
        [HttpPatch("api/v1/restaurants/{restaurantId:int}/staff/{staffId:int}/activate")]
        public async Task<IActionResult> ActivateStaff(int restaurantId, int staffId)
        {
            await Mediator.Send(new ActivateStaffCommand(restaurantId, staffId));
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageStaff)]
        [HttpPatch("api/v1/restaurants/{restaurantId:int}/staff/{staffId:int}/deactivate")]
        public async Task<IActionResult> DeactivateStaff(int restaurantId, int staffId)
        {
            await Mediator.Send(new DeactivateStaffCommand(restaurantId, staffId));
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageStaff)]
        [HttpPut("api/v1/restaurants/{restaurantId:int}/staff/{staffId:int}")]
        public async Task<IActionResult> UpdateStaff(int restaurantId, int staffId, [FromForm] UpdateStaffCommand command)
        {
            command.RestaurantId = restaurantId;
            command.StaffId = staffId;

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageStaff)]
        [HttpPatch("api/v1/users/{userId:int}/role")]
        public async Task<IActionResult> UpdateRole(int userId, [FromQuery] int roleId)
        {
            var command = new UpdateRoleCommand
            {
                UserId = userId,
                RoleId = roleId
            };
            return Ok(await Mediator.Send(command));
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageUsers)]
        [HttpGet("api/v1/admin/users")]
        public async Task<IActionResult> GetAll([FromQuery] GetAllUsersQuery query)
        {
            var result = await Mediator.Send(query);
            return Ok(result);
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantInfo)]
        //[RequireActiveRestaurantContract]
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
