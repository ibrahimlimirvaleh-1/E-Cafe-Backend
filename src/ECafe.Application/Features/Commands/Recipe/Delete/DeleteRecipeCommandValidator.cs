using FluentValidation;

namespace ECafe.Application.Features.Commands.Recipe.Delete
{
    public sealed class DeleteRecipeCommandValidator : AbstractValidator<DeleteRecipeCommand>
    {
        public DeleteRecipeCommandValidator()
        {
            RuleFor(x => x.RestaurantId).GreaterThan(0);
            RuleFor(x => x.ItemId).GreaterThan(0);
            RuleFor(x => x.RecipeId).GreaterThan(0);
        }
    }
}
