using MediatR;

namespace ECafe.Application.Features.Commands.User.ActivateStaff
{
    public sealed record ActivateStaffCommand(int RestaurantId, int StaffId) : IRequest;
}
