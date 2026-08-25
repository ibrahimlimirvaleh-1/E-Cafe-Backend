using FluentValidation;

namespace ECafe.Application.Features.Commands.Auth.ForgotPassword;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(150).WithMessage("Email must be at most 150 characters.")
            .EmailAddress().WithMessage("Email format is invalid.");
    }
}
