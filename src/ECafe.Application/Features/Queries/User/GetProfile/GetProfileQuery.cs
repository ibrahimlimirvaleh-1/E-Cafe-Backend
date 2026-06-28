using ECafe.Application.DTOs.User;
using ECafe.Application.Services.User.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.User.GetProfile
{
    public record GetProfileQuery(int UserId) : IRequest<ProfileResponseDto>;

    public class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, ProfileResponseDto>
    {
        private readonly IUserService _userService;

        public GetProfileQueryHandler(IUserService userService)
        {
            _userService = userService;
        }

        public Task<ProfileResponseDto> Handle(GetProfileQuery request, CancellationToken cancellationToken)
            => _userService.GetProfileAsync(request.UserId);
    }
}
