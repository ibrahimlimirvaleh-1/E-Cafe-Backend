using ECafe.Application.DTOs.Auth;
using ECafe.Application.Services.Auth.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.Register
{
    public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, AuthResponseDto>
    {
        private readonly IAuthService _authService;

        public RegisterUserCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<AuthResponseDto> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
        {
            return await _authService.RegisterAsync(request);
        }
    }
}
