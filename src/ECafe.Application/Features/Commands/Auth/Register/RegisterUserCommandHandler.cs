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
            var dto = new RegisterRequestDto
            {
                Name = request.Name,
                Surname = request.Surname,
                Email = request.Email,
                Phone = request.Phone,
                Password = request.Password,
                Image = request.Image
            };

            return await _authService.RegisterAsync(dto);
        }
    }
}
