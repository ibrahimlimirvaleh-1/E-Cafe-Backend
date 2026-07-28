using FluentValidation;

namespace ECafe.Application.Features.Commands.InventoryItem.Update
{
    public sealed class UpdateInventoryItemCommandValidator : AbstractValidator<UpdateInventoryItemCommand>
    {
        public UpdateInventoryItemCommandValidator()
        {
            RuleFor(x => x.RestaurantId).GreaterThan(0);
            RuleFor(x => x.InventoryItemId).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            RuleFor(x => x.UnitId).GreaterThan(0);
            RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0);
        }
    }
}
