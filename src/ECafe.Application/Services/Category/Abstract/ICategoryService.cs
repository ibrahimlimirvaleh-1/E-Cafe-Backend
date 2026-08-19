using ECafe.Application.DTOs.Category;

namespace ECafe.Application.Services.Category.Abstract
{
    public interface ICategoryService
    {
        public Task<List<GetAllCategoryResponse>> GetCategoriesByRestaurantIdAsync(int restaurantId);

        public Task<int> CreateCategoryAsync(CreateCategoryRequest request);

        Task<GetAllCategoryResponse> UpdateCategoryAsync(int restaurantId, int categoryId, UpdateCategoryRequest request);

        Task<GetAllCategoryResponse> ActivateCategoryAsync(int restaurantId, int categoryId);

        Task<GetAllCategoryResponse> DeactivateCategoryAsync(int restaurantId, int categoryId);

        Task<GetAllCategoryResponse> DeleteCategoryAsync(int restaurantId, int categoryId);
    }
}
