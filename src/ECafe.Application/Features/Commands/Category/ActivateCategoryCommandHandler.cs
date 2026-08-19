using ECafe.Application.DTOs.Category;
using ECafe.Application.Services.Category.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Category
{
    public class ActivateCategoryCommandHandler : IRequestHandler<ActivateCategoryCommand, GetAllCategoryResponse>
    {
        private readonly ICategoryService _categoryService;

        public ActivateCategoryCommandHandler(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public Task<GetAllCategoryResponse> Handle(ActivateCategoryCommand request, CancellationToken cancellationToken)
        {
            return _categoryService.ActivateCategoryAsync(request.RestaurantId, request.CategoryId);
        }
    }
}
