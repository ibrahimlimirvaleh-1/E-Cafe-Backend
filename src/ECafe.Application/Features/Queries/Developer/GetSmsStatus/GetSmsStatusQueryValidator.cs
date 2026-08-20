using FluentValidation;

namespace ECafe.Application.Features.Queries.Developer.GetSmsStatus;

public sealed class GetSmsStatusQueryValidator : AbstractValidator<GetSmsStatusQuery>
{
    public GetSmsStatusQueryValidator()
    {
        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("SMS message ID is required.")
            .MaximumLength(128).WithMessage("SMS message ID must be at most 128 characters.");
    }
}
