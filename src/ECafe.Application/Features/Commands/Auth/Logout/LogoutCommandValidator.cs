using FluentValidation;

namespace ECafe.Application.Features.Commands.Auth.Logout;

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.RefreshToken));
    }
}
