using ECafe.Application.DTOs.Item;
using MediatR;

namespace ECafe.Application.Features.Commands.Item
{
    public class CreateItemCommand : CreateItemRequest, IRequest<int>
    {
    }
}
