using ECafe.Application.Common.Validation;
using FluentValidation;

namespace ECafe.Application.Features.Commands.Restaurant
{
    public sealed class RegisterRestaurantCommandValidator : AbstractValidator<RegisterRestaurantCommand>
    {
        public RegisterRestaurantCommandValidator()
        {
            RuleFor(x => x.Phone)
                .MustBePhoneNumber("Restaurant phone");

            RuleFor(x => x.BranchName)
                .NotEmpty()
                .WithMessage("Branch name is required.");

            RuleFor(x => x)
                .Must(x => x.RestaurantGroupId.GetValueOrDefault() > 0 || !string.IsNullOrWhiteSpace(x.RestaurantGroupName))
                .WithMessage("Restaurant group is required.");

            RuleFor(x => x.DefaultWaiterTableLimit)
                .GreaterThan(0)
                .When(x => x.DefaultWaiterTableLimit.HasValue)
                .WithMessage("Default waiter table limit must be greater than 0.");
        }
    }
}
