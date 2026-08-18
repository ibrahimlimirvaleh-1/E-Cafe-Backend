using ECafe.Application.DTOs.Category;
using ECafe.Application.Services.Category.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Category
{
    public class DeactivateCategoryCommandHandler : IRequestHandler<DeactivateCategoryCommand, GetAllCategoryResponse>
    {
        private readonly ICategoryService _categoryService;

        public DeactivateCategoryCommandHandler(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public Task<GetAllCategoryResponse> Handle(DeactivateCategoryCommand request, CancellationToken cancellationToken)
        {
            return _categoryService.DeactivateCategoryAsync(request.RestaurantId, request.CategoryId);
        }
    }
}
