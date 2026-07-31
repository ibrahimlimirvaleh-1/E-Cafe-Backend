using FluentValidation;

namespace ECafe.Application.Features.Commands.InventoryMovement.Create
{
    public sealed class CreateInventoryMovementCommandValidator : AbstractValidator<CreateInventoryMovementCommand>
    {
        public CreateInventoryMovementCommandValidator()
        {
            RuleFor(x => x.RestaurantId).GreaterThan(0);
            RuleFor(x => x.InventoryItemId).GreaterThan(0);
            RuleFor(x => x.Quantity).GreaterThan(0);
            RuleFor(x => x.UnitId).GreaterThan(0);
            RuleFor(x => x.MovementTypeId).GreaterThan(0);
            RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        }
    }
}
