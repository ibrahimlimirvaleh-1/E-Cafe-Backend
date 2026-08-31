using ECafe.Application.Services.Item.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Item
{
    public class UpdateItemCommandHandler : IRequestHandler<UpdateItemCommand, int>
    {
        private readonly IItemService _itemService;

        public UpdateItemCommandHandler(IItemService itemService)
        {
            _itemService = itemService;
        }

        public Task<int> Handle(UpdateItemCommand request, CancellationToken cancellationToken)
            => _itemService.UpdateAsync(request.RestaurantId, request.ItemId, request);
    }
}
