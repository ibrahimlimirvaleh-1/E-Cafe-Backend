using ECafe.Application.Services.Auth.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IPasswordResetService _passwordResetService;

    public ForgotPasswordCommandHandler(IPasswordResetService passwordResetService)
    {
        _passwordResetService = passwordResetService;
    }

    public Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        => _passwordResetService.RequestPasswordResetAsync(request);
}
