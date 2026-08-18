using ECafe.Application.DTOs.Category;
using ECafe.Application.Services.Category.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Category
{
    public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, GetAllCategoryResponse>
    {
        private readonly ICategoryService _categoryService;

        public UpdateCategoryCommandHandler(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public Task<GetAllCategoryResponse> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
        {
            return _categoryService.UpdateCategoryAsync(request.RestaurantId, request.CategoryId, request);
        }
    }
}
