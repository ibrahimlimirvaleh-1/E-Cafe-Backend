using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Services.Recipe.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Recipe.Deactivate
{
    public class DeactivateRecipeCommandHandler : IRequestHandler<DeactivateRecipeCommand, DeleteOrDeactivateResponse>
    {
        private readonly IRecipeService _recipeService;

        public DeactivateRecipeCommandHandler(IRecipeService recipeService)
        {
            _recipeService = recipeService;
        }

        public Task<DeleteOrDeactivateResponse> Handle(DeactivateRecipeCommand request, CancellationToken cancellationToken)
            => _recipeService.DeactivateAsync(request.RestaurantId, request.ItemId, request.RecipeId);
    }
}
