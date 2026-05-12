using ECafe.Application.DTOs.Auth;
using ECafe.Application.Services.Auth.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.Login
{
    public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, string>
    {
        private readonly IAuthService _authService;

        public LoginUserCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<string> Handle(LoginUserCommand request, CancellationToken cancellationToken)
        {
            var dto = new LoginRequestDto
            {
                Email = request.Email,
                Password = request.Password
            };

            return await _authService.LoginAsync(dto);
        }
    }
}
