using ECafe.Application.DTOs.Auth;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.MobileLogout;

public sealed class MobileLogoutCommand : LogoutRequestDto, IRequest
{
}
