using MediatR;

namespace ECafe.Application.Features.Commands.User.DeactivateStaff
{
    public sealed record DeactivateStaffCommand(int RestaurantId, int StaffId) : IRequest;
}
