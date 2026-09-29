using ECafe.Application.Common.Validation;
using FluentValidation;

namespace ECafe.Application.Features.Commands.Restaurant;

public sealed class SetRestaurantDepositRuleCommandValidator : AbstractValidator<SetRestaurantDepositRuleCommand>
{
    public SetRestaurantDepositRuleCommandValidator()
    {
        RuleFor(x => x.Amount)
            .Must(RestaurantDepositAmount.IsValid)
            .WithMessage(RestaurantDepositAmount.InvalidAmountMessage);
    }
}
