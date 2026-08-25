using ECafe.Application.Services.Auth.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IPasswordResetService _passwordResetService;

    public ResetPasswordCommandHandler(IPasswordResetService passwordResetService)
    {
        _passwordResetService = passwordResetService;
    }

    public Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        => _passwordResetService.ResetPasswordAsync(request);
}
