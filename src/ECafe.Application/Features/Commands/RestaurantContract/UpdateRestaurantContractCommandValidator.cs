using FluentValidation;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public sealed class UpdateRestaurantContractCommandValidator : AbstractValidator<UpdateRestaurantContractCommand>
    {
        public UpdateRestaurantContractCommandValidator()
        {
            Include(new RestaurantContractDateRequestValidator<UpdateRestaurantContractCommand>());
            RuleFor(x => x.RestaurantId).GreaterThan(0);
            RuleFor(x => x.ContractId).GreaterThan(0);
        }
    }
}
