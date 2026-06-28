using ECafe.Application.DTOs.User.Staff;
using ECafe.Application.Services.User.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.User.GetStaffDetail
{
    public record GetStaffDetailQuery(int RestaurantId, int StaffId) : IRequest<StaffDetailResponseDto>;

    public class GetStaffDetailQueryHandler : IRequestHandler<GetStaffDetailQuery, StaffDetailResponseDto>
    {
        private readonly IUserService _userService;

        public GetStaffDetailQueryHandler(IUserService userService)
        {
            _userService = userService;
        }

        public Task<StaffDetailResponseDto> Handle(GetStaffDetailQuery request, CancellationToken cancellationToken)
            => _userService.GetStaffDetailAsync(request.RestaurantId, request.StaffId);
    }
}
