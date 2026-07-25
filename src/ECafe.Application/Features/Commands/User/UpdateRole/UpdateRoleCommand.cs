using ECafe.Application.DTOs.Auth;
using MediatR;

namespace ECafe.Application.Features.Commands.User.UpdateRole
{
    public class UpdateRoleCommand : IRequest<AuthResponseDto>
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
    }
}
