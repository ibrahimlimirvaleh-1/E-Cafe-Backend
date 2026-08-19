using FluentValidation;

namespace ECafe.Application.Features.Commands.Developer.SendTestEmail;

public sealed class SendTestEmailCommandValidator : AbstractValidator<SendTestEmailCommand>
{
    public SendTestEmailCommandValidator()
    {
        RuleFor(x => x.ToEmail)
            .NotEmpty().WithMessage("Email recipient is required.")
            .EmailAddress().WithMessage("Email recipient format is invalid.")
            .MaximumLength(256).WithMessage("Email recipient must be at most 256 characters.");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Email subject is required.")
            .MaximumLength(200).WithMessage("Email subject must be at most 200 characters.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Email body is required.")
            .MaximumLength(4000).WithMessage("Email body must be at most 4000 characters.");
    }
}
