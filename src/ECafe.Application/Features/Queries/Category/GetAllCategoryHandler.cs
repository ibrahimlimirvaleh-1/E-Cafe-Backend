using ECafe.Application.DTOs.Category;
using ECafe.Application.Services.Category.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Category
{
    public record GetAllCategoryQuery(int RestaurantId) : IRequest<List<GetAllCategoryResponse>>;
    public class GetAllCategoryHandler : IRequestHandler<GetAllCategoryQuery, List<GetAllCategoryResponse>>
    {
        private readonly ICategoryService _categoryService;

        public GetAllCategoryHandler(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public async Task<List<GetAllCategoryResponse>> Handle(GetAllCategoryQuery request, CancellationToken cancellationToken)
        {
            return await _categoryService.GetCategoriesByRestaurantIdAsync(request.RestaurantId);
        }
    }
}
