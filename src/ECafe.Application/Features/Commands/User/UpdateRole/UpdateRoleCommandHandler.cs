using ECafe.Application.DTOs.Auth;
using ECafe.Application.Services.User.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.User.UpdateRole
{
    public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, AuthResponseDto>
    {
        private readonly IUserService _userService;

        public UpdateRoleCommandHandler(IUserService userService)
        {
            _userService = userService;
        }

        public Task<AuthResponseDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
            => _userService.UpdateRoleAsync(request.UserId, request.RoleId);
    }
}
