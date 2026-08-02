using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Services.Recipe.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Recipe.Activate
{
    public class ActivateRecipeCommandHandler : IRequestHandler<ActivateRecipeCommand, DeleteOrDeactivateResponse>
    {
        private readonly IRecipeService _recipeService;

        public ActivateRecipeCommandHandler(IRecipeService recipeService)
        {
            _recipeService = recipeService;
        }

        public Task<DeleteOrDeactivateResponse> Handle(ActivateRecipeCommand request, CancellationToken cancellationToken)
            => _recipeService.ActivateAsync(request.RestaurantId, request.ItemId, request.RecipeId);
    }
}
