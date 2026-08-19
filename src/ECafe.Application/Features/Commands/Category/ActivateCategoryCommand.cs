using ECafe.Application.DTOs.Category;
using MediatR;

namespace ECafe.Application.Features.Commands.Category
{
    public class ActivateCategoryCommand : IRequest<GetAllCategoryResponse>
    {
        public int RestaurantId { get; set; }
        public int CategoryId { get; set; }
    }
}
