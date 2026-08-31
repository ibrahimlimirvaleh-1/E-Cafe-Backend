using ECafe.Application.Services.Item.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Item
{
    public class DeleteItemCommandHandler : IRequestHandler<DeleteItemCommand, int>
    {
        private readonly IItemService _itemService;

        public DeleteItemCommandHandler(IItemService itemService)
        {
            _itemService = itemService;
        }

        public Task<int> Handle(DeleteItemCommand request, CancellationToken cancellationToken)
            => _itemService.DeleteAsync(request.RestaurantId, request.ItemId);
    }
}
