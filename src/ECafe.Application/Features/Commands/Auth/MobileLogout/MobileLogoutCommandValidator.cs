using FluentValidation;

namespace ECafe.Application.Features.Commands.Auth.MobileLogout;

public sealed class MobileLogoutCommandValidator : AbstractValidator<MobileLogoutCommand>
{
    public MobileLogoutCommandValidator()
    {
        RuleFor(request => request.RefreshToken)
            .MaximumLength(1000)
            .When(request => !string.IsNullOrWhiteSpace(request.RefreshToken));
    }
}
