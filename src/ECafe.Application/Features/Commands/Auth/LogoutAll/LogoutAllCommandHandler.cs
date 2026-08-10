using ECafe.Application.Services.Auth.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.LogoutAll;

public class LogoutAllCommandHandler : IRequestHandler<LogoutAllCommand>
{
    private readonly IAuthService _authService;

    public LogoutAllCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task Handle(LogoutAllCommand request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAllAsync();
    }
}
