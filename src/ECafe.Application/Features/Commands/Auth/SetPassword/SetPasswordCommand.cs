using ECafe.Application.DTOs.Auth;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.SetPassword
{
    public class SetPasswordCommand : SetPasswordRequest, IRequest
    {
    }
}
