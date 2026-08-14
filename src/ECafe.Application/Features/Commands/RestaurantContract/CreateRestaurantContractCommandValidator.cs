using FluentValidation;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public sealed class CreateRestaurantContractCommandValidator : AbstractValidator<CreateRestaurantContractCommand>
    {
        public CreateRestaurantContractCommandValidator()
        {
            Include(new RestaurantContractDateRequestValidator<CreateRestaurantContractCommand>());
            RuleFor(x => x.RestaurantId).GreaterThan(0);
        }
    }
}
