using ECafe.Application.Services.Auth.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.RevokeSession;

public sealed record RevokeSessionCommand(string SessionId) : IRequest;

public sealed class RevokeSessionCommandHandler : IRequestHandler<RevokeSessionCommand>
{
    private readonly IUserSessionService _userSessionService;

    public RevokeSessionCommandHandler(IUserSessionService userSessionService)
    {
        _userSessionService = userSessionService;
    }

    public Task Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
        => _userSessionService.RevokeMySessionAsync(request.SessionId);
}
