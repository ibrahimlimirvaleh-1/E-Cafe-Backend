using ECafe.Application.Common.Validation;
using FluentValidation;

namespace ECafe.Application.Features.Commands.Developer.SendTestSms;

public sealed class SendTestSmsCommandValidator : AbstractValidator<SendTestSmsCommand>
{
    public SendTestSmsCommandValidator()
    {
        RuleFor(x => x.ToPhone)
            .MustBePhoneNumber("Phone recipient");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("SMS message is required.")
            .MaximumLength(320).WithMessage("SMS message must be at most 320 characters.");
    }
}
