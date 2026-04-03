using MediatR;

namespace ECafe.Application.Features.Commands.User.UpdateRole
{
    public class UpdateRoleCommand : IRequest
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
    }
}
