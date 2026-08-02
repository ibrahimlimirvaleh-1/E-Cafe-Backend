using ECafe.Application.DTOs.User;
using ECafe.Application.Services.User.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.User.GetAll
{
    public class GetAllUsersQuery : IRequest<PaginatedList<GetAllUserResponseDto>>
    {
        public int? RestaurantId { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, PaginatedList<GetAllUserResponseDto>>
        {
            private readonly IUserService _userService;

            public GetAllUsersQueryHandler(IUserService userService)
            {
                _userService = userService;
            }

            public Task<PaginatedList<GetAllUserResponseDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
            {
                var filter = new PaginationFilter(request.PageNumber, request.PageSize);
                return _userService.GetAllAsync(request.RestaurantId, filter);
            }
        }
    }
}
