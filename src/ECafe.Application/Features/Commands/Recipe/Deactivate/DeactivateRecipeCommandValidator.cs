using FluentValidation;

namespace ECafe.Application.Features.Commands.Recipe.Deactivate
{
    public sealed class DeactivateRecipeCommandValidator : AbstractValidator<DeactivateRecipeCommand>
    {
        public DeactivateRecipeCommandValidator()
        {
            RuleFor(x => x.RestaurantId).GreaterThan(0);
            RuleFor(x => x.ItemId).GreaterThan(0);
            RuleFor(x => x.RecipeId).GreaterThan(0);
        }
    }
}
