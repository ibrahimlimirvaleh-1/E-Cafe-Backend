using ECafe.Application.DTOs.Category;
using ECafe.Application.Services.Category.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Category
{
    public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, int>
    {
        private readonly ICategoryService _categoryService;

        public CreateCategoryCommandHandler(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public async Task<int> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
            var dto = new CreateCategoryRequest
            {
                RestaurantId = request.RestaurantId,
                Name = request.Name,
                SortOrder = request.SortOrder
            };

            return await _categoryService.CreateCategoryAsync(dto);
        }
    }
}
