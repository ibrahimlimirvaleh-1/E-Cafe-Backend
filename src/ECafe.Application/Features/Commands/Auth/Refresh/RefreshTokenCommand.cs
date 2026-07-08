using ECafe.Application.DTOs.Auth;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.Refresh
{
    public class RefreshTokenCommand : RefreshTokenRequestDto, IRequest<AuthResponseDto>
    {
    }
}
