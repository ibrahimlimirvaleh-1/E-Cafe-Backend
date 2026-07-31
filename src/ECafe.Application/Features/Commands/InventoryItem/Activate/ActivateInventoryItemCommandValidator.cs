using FluentValidation;

namespace ECafe.Application.Features.Commands.InventoryItem.Activate
{
    public sealed class ActivateInventoryItemCommandValidator : AbstractValidator<ActivateInventoryItemCommand>
    {
        public ActivateInventoryItemCommandValidator()
        {
            RuleFor(x => x.RestaurantId)
                .GreaterThan(0)
                .WithMessage("RestaurantId must be greater than 0.");

            RuleFor(x => x.InventoryItemId)
                .GreaterThan(0)
                .WithMessage("InventoryItemId must be greater than 0.");
        }
    }
}
