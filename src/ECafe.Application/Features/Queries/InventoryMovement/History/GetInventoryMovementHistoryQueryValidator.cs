using FluentValidation;

namespace ECafe.Application.Features.Queries.InventoryMovement.History
{
    public sealed class GetInventoryMovementHistoryQueryValidator
        : AbstractValidator<GetInventoryMovementHistoryQuery>
    {
        public GetInventoryMovementHistoryQueryValidator()
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
