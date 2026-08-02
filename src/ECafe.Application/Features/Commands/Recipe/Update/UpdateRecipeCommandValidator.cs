using FluentValidation;

namespace ECafe.Application.Features.Commands.Recipe.Update
{
    public sealed class UpdateRecipeCommandValidator : AbstractValidator<UpdateRecipeCommand>
    {
        public UpdateRecipeCommandValidator()
        {
            RuleFor(x => x.RestaurantId).GreaterThan(0);
            RuleFor(x => x.ItemId).GreaterThan(0);
            RuleFor(x => x.RecipeId).GreaterThan(0);
            RuleFor(x => x.InventoryItemId).GreaterThan(0);
            RuleFor(x => x.Quantity).GreaterThan(0);
            RuleFor(x => x.UnitId).GreaterThan(0);
        }
    }
}
