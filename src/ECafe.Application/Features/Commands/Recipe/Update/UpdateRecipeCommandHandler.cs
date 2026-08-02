using ECafe.Application.DTOs.Recipe;
using ECafe.Application.Services.Recipe.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Recipe.Update
{
    public class UpdateRecipeCommandHandler : IRequestHandler<UpdateRecipeCommand, RecipeDto>
    {
        private readonly IRecipeService _recipeService;

        public UpdateRecipeCommandHandler(IRecipeService recipeService)
        {
            _recipeService = recipeService;
        }

        public Task<RecipeDto> Handle(UpdateRecipeCommand request, CancellationToken cancellationToken)
            => _recipeService.UpdateAsync(request.RestaurantId, request.ItemId, request.RecipeId, request);
    }
}
