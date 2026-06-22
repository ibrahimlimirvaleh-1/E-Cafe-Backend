using ECafe.Application.Services.Item.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Item
{
    public class CreateItemCommandHandler : IRequestHandler<CreateItemCommand, int>
    {
        private readonly IItemService _itemService;

        public CreateItemCommandHandler(IItemService itemService)
        {
            _itemService = itemService;
        }

        public async Task<int> Handle(CreateItemCommand request, CancellationToken cancellationToken)
        {
            return await _itemService.CreateAsync(request);
        }
    }
}
