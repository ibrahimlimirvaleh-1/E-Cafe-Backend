using ECafe.Application.DTOs.Item;
using MediatR;

namespace ECafe.Application.Features.Commands.Item
{
    public class UpdateItemCommand : UpdateItemRequest, IRequest<int>
    {
        public int RestaurantId { get; set; }

        public int ItemId { get; set; }
    }
}
