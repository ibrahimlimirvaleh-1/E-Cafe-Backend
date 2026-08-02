using ECafe.Application.DTOs.Recipe;
using MediatR;

namespace ECafe.Application.Features.Commands.Recipe.Update
{
    public class UpdateRecipeCommand : UpdateRecipeRequest, IRequest<RecipeDto>
    {
        public int RestaurantId { get; set; }
        public int ItemId { get; set; }
        public int RecipeId { get; set; }
    }
}
