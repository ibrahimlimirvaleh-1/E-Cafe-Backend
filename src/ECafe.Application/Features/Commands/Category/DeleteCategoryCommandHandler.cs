using ECafe.Application.DTOs.Category;
using ECafe.Application.Services.Category.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Category
{
    public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, GetAllCategoryResponse>
    {
        private readonly ICategoryService _categoryService;

        public DeleteCategoryCommandHandler(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public Task<GetAllCategoryResponse> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
        {
            return _categoryService.DeleteCategoryAsync(request.RestaurantId, request.CategoryId);
        }
    }
}
