using ECafe.Application.DTOs.Auth;
using ECafe.Application.Services.Auth.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Auth.GetMySessions;

public sealed record GetMySessionsQuery : IRequest<List<UserSessionResponseDto>>;

public sealed class GetMySessionsQueryHandler : IRequestHandler<GetMySessionsQuery, List<UserSessionResponseDto>>
{
    private readonly IUserSessionService _userSessionService;

    public GetMySessionsQueryHandler(IUserSessionService userSessionService)
    {
        _userSessionService = userSessionService;
    }

    public Task<List<UserSessionResponseDto>> Handle(GetMySessionsQuery request, CancellationToken cancellationToken)
        => _userSessionService.GetMySessionsAsync();
}
