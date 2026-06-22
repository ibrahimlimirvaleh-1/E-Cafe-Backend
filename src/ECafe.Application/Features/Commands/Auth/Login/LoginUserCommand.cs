using ECafe.Application.DTOs.Auth;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.Login
{
    public class LoginUserCommand : LoginRequestDto, IRequest<string>
    {
    }
}
