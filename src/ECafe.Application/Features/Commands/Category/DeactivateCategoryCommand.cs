using ECafe.Application.DTOs.Category;
using MediatR;

namespace ECafe.Application.Features.Commands.Category
{
    public class DeactivateCategoryCommand : IRequest<GetAllCategoryResponse>
    {
        public int RestaurantId { get; set; }
        public int CategoryId { get; set; }
    }
}
