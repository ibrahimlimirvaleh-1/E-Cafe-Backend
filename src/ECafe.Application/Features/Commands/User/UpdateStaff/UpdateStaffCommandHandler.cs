using ECafe.Application.DTOs.User.Staff;
using ECafe.Application.Services.User.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.User.UpdateStaff
{
    public sealed class UpdateStaffCommandHandler : IRequestHandler<UpdateStaffCommand, StaffDetailResponseDto>
    {
        private readonly IUserService _userService;

        public UpdateStaffCommandHandler(IUserService userService)
        {
            _userService = userService;
        }

        public Task<StaffDetailResponseDto> Handle(UpdateStaffCommand request, CancellationToken cancellationToken)
        {
            return _userService.UpdateStaffAsync(request.RestaurantId, request.StaffId, request);
        }
    }
}
