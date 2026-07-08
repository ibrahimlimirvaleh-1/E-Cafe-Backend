using ECafe.Application.DTOs.Auth;
using ECafe.Application.Services.Auth.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.Refresh
{
    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
    {
        private readonly IAuthService _authService;

        public RefreshTokenCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            return await _authService.RefreshAsync(request);
        }
    }
}
