using ECafe.Application.DTOs.User.Staff;
using MediatR;

namespace ECafe.Application.Features.Commands.User.UpdateStaff
{
    public sealed class UpdateStaffCommand : UpdateStaffRequest, IRequest<StaffDetailResponseDto>
    {
        public int RestaurantId { get; set; }

        public int StaffId { get; set; }
    }
}
