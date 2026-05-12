using ECafe.Application.DTOs.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.Features.Commands.Auth.Register
{
    public class RegisterUserCommand : IRequest<AuthResponseDto>
    {
        public string Name { get; set; } = null!;

        public string Surname { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string Phone { get; set; } = null!;

        public string Password { get; set; } = null!;
        public IFormFile? Image { get; set; }
    }
}
