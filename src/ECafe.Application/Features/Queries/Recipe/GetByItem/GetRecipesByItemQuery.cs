using ECafe.Application.DTOs.Recipe;
using ECafe.Application.Services.Recipe.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Recipe.GetByItem
{
    public class GetRecipesByItemQuery : IRequest<List<RecipeDto>>
    {
        public int RestaurantId { get; set; }
        public int ItemId { get; set; }
    }

    public class GetRecipesByItemQueryHandler : IRequestHandler<GetRecipesByItemQuery, List<RecipeDto>>
    {
        private readonly IRecipeService _recipeService;

        public GetRecipesByItemQueryHandler(IRecipeService recipeService)
        {
            _recipeService = recipeService;
        }

        public Task<List<RecipeDto>> Handle(GetRecipesByItemQuery request, CancellationToken cancellationToken)
            => _recipeService.GetByItemAsync(request.RestaurantId, request.ItemId);
    }
}
