using FluentValidation;

namespace ECafe.Application.Features.Commands.InventoryItem.Create
{
    public sealed class CreateInventoryItemCommandValidator : AbstractValidator<CreateInventoryItemCommand>
    {
        public CreateInventoryItemCommandValidator()
        {
            RuleFor(x => x.RestaurantId).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            RuleFor(x => x.UnitId).GreaterThan(0);
            RuleFor(x => x.QuantityOnHand).GreaterThanOrEqualTo(0);
            RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0);
        }
    }
}