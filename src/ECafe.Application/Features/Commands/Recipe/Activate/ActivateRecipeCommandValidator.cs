using FluentValidation;

namespace ECafe.Application.Features.Commands.Recipe.Activate
{
    public sealed class ActivateRecipeCommandValidator : AbstractValidator<ActivateRecipeCommand>
    {
        public ActivateRecipeCommandValidator()
        {
            RuleFor(x => x.RestaurantId).GreaterThan(0);
            RuleFor(x => x.ItemId).GreaterThan(0);
            RuleFor(x => x.RecipeId).GreaterThan(0);
        }
    }
}
