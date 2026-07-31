using FluentValidation;

namespace ECafe.Application.Features.Commands.InventoryItem.Delete
{
    public sealed class DeleteInventoryItemCommandValidator : AbstractValidator<DeleteInventoryItemCommand>
    {
        public DeleteInventoryItemCommandValidator()
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
