using ECafe.Application.Services.User.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.User.DeactivateStaff
{
    public sealed class DeactivateStaffCommandHandler : IRequestHandler<DeactivateStaffCommand>
    {
        private readonly IUserService _userService;

        public DeactivateStaffCommandHandler(IUserService userService)
        {
            _userService = userService;
        }

        public Task Handle(DeactivateStaffCommand request, CancellationToken cancellationToken)
            => _userService.DeactivateStaffAsync(request.RestaurantId, request.StaffId);
    }
}
