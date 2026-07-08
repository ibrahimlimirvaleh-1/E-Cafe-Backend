using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.DTOs.Auth;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.Login
{
    public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, AuthResponseDto>
    {
        private readonly IAuthService _authService;

        public LoginUserCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<AuthResponseDto> Handle(LoginUserCommand request, CancellationToken cancellationToken)
        {
            return await _authService.LoginAsync(request);
        }
    }
}
