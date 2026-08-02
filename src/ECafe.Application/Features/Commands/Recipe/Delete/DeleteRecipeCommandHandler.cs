using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Services.Recipe.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Recipe.Delete
{
    public class DeleteRecipeCommandHandler : IRequestHandler<DeleteRecipeCommand, DeleteOrDeactivateResponse>
    {
        private readonly IRecipeService _recipeService;

        public DeleteRecipeCommandHandler(IRecipeService recipeService)
        {
            _recipeService = recipeService;
        }

        public Task<DeleteOrDeactivateResponse> Handle(DeleteRecipeCommand request, CancellationToken cancellationToken)
            => _recipeService.DeleteAsync(request.RestaurantId, request.ItemId, request.RecipeId);
    }
}
