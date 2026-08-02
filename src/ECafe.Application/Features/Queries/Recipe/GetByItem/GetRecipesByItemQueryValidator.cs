using FluentValidation;

namespace ECafe.Application.Features.Queries.Recipe.GetByItem
{
    public sealed class GetRecipesByItemQueryValidator : AbstractValidator<GetRecipesByItemQuery>
    {
        public GetRecipesByItemQueryValidator()
        {
            RuleFor(x => x.RestaurantId).GreaterThan(0);
            RuleFor(x => x.ItemId).GreaterThan(0);
        }
    }
}
