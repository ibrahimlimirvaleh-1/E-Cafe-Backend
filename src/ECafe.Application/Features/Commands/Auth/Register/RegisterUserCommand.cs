using ECafe.Application.DTOs.Auth;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.Register
{
    public class RegisterUserCommand : RegisterRequestDto, IRequest<AuthResponseDto>
    {
    }
}
