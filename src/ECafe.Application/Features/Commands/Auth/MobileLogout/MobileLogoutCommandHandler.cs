using ECafe.Application.Services.Auth.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.MobileLogout;

public sealed class MobileLogoutCommandHandler(IAuthService auth) : IRequestHandler<MobileLogoutCommand>
{
    /// <summary>Native çıxış əmrini access token tələb etməyən sessiya-ailəsi ləğvinə yönəldir.</summary>
    public Task Handle(MobileLogoutCommand request, CancellationToken cancellationToken)
        => auth.LogoutSessionAsync(request);
}
