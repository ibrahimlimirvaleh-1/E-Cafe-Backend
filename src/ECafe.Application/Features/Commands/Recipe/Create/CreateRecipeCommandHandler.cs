using ECafe.Application.DTOs.Recipe;
using ECafe.Application.Services.Recipe.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Recipe.Create
{
    public class CreateRecipeCommandHandler : IRequestHandler<CreateRecipeCommand, RecipeDto>
    {
        private readonly IRecipeService _recipeService;

        public CreateRecipeCommandHandler(IRecipeService recipeService)
        {
            _recipeService = recipeService;
        }

        public Task<RecipeDto> Handle(CreateRecipeCommand request, CancellationToken cancellationToken)
            => _recipeService.CreateAsync(request.RestaurantId, request.ItemId, request);
    }
}
