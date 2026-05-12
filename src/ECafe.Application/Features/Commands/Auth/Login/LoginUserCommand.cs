using MediatR;

namespace ECafe.Application.Features.Commands.Auth.Login
{
    public class LoginUserCommand : IRequest<string>
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}