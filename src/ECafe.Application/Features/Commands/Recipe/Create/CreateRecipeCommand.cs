using ECafe.Application.DTOs.Recipe;
using MediatR;

namespace ECafe.Application.Features.Commands.Recipe.Create
{
    public class CreateRecipeCommand : CreateRecipeRequest, IRequest<RecipeDto>
    {
        public int RestaurantId { get; set; }
        public int ItemId { get; set; }
    }
}
