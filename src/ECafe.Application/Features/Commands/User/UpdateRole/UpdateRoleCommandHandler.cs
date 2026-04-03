using ECafe.Application.Services.User.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.User.UpdateRole
{
    public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand>
    {
        private readonly IUserService _userService;

        public UpdateRoleCommandHandler(IUserService userService)
        {
            _userService = userService;
        }

        public async Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
        {
            await _userService.UpdateRoleAsync(request.UserId, request.RoleId);
        }
    }
}
