using ECafe.Application.DTOs.InventoryItem;
using MediatR;

namespace ECafe.Application.Features.Commands.Recipe.Activate
{
    public class ActivateRecipeCommand : IRequest<DeleteOrDeactivateResponse>
    {
        public int RestaurantId { get; set; }
        public int ItemId { get; set; }
        public int RecipeId { get; set; }
    }
}
