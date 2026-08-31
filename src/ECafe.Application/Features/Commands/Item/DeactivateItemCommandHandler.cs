using ECafe.Application.Services.Item.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Item
{
    public class DeactivateItemCommandHandler : IRequestHandler<DeactivateItemCommand, int>
    {
        private readonly IItemService _itemService;

        public DeactivateItemCommandHandler(IItemService itemService)
        {
            _itemService = itemService;
        }

        public Task<int> Handle(DeactivateItemCommand request, CancellationToken cancellationToken)
            => _itemService.DeactivateAsync(request.RestaurantId, request.ItemId);
    }
}
