using ECafe.Application.DTOs.Category;

namespace ECafe.Application.Services.Category.Abstract
{
    public interface ICategoryService
    {
        public Task<List<GetAllCategoryResponse>> GetCategoriesByRestaurantIdAsync(int restaurantId);

        public Task<int> CreateCategoryAsync(CreateCategoryRequest request);
    }
}
