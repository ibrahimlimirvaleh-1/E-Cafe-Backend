using ECafe.Application.Services.User.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.User.ActivateStaff
{
    public sealed class ActivateStaffCommandHandler : IRequestHandler<ActivateStaffCommand>
    {
        private readonly IUserService _userService;

        public ActivateStaffCommandHandler(IUserService userService)
        {
            _userService = userService;
        }

        public Task Handle(ActivateStaffCommand request, CancellationToken cancellationToken)
            => _userService.ActivateStaffAsync(request.RestaurantId, request.StaffId);
    }
}
