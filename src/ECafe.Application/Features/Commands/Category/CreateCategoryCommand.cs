using ECafe.Application.DTOs.Category;
using MediatR;

namespace ECafe.Application.Features.Commands.Category
{
    public class CreateCategoryCommand : CreateCategoryRequest, IRequest<int>
    {
    }
}