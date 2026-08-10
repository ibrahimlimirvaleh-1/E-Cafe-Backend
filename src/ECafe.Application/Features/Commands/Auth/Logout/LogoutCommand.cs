using ECafe.Application.DTOs.Auth;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.Logout;

public class LogoutCommand : LogoutRequestDto, IRequest
{
}
