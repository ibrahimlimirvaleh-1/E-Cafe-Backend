using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.DTOs.Recipe;

namespace ECafe.Application.Services.Recipe.Abstract
{
    public interface IRecipeService
    {
        Task<List<RecipeDto>> GetByItemAsync(int restaurantId, int itemId);
        Task<RecipeDto> CreateAsync(int restaurantId, int itemId, CreateRecipeRequest request);
        Task<RecipeDto> UpdateAsync(int restaurantId, int itemId, int recipeId, UpdateRecipeRequest request);
        Task<DeleteOrDeactivateResponse> ActivateAsync(int restaurantId, int itemId, int recipeId);
        Task<DeleteOrDeactivateResponse> DeactivateAsync(int restaurantId, int itemId, int recipeId);
        Task<DeleteOrDeactivateResponse> DeleteAsync(int restaurantId, int itemId, int recipeId);
    }
}
