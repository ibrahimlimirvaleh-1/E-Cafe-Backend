using MediatR;

namespace ECafe.Application.Features.Commands.Item
{
    public class DeactivateItemCommand : IRequest<int>
    {
        public int RestaurantId { get; set; }

        public int ItemId { get; set; }
    }
}
