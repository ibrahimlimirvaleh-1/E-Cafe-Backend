using FluentValidation;

namespace ECafe.Application.Features.Commands.InventoryItem.Deactivate
{
    public sealed class DeactivateInventoryItemCommandValidator : AbstractValidator<DeactivateInventoryItemCommand>
    {
        public DeactivateInventoryItemCommandValidator()
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
