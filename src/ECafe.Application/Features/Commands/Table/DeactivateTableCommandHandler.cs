using ECafe.Application.DTOs.Table;
using ECafe.Application.Services.Table.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Table
{
    public class DeactivateTableCommandHandler : IRequestHandler<DeactivateTableCommand, TableResponse>
    {
        private readonly ITableService _tableService;

        public DeactivateTableCommandHandler(ITableService tableService)
        {
            _tableService = tableService;
        }

        public Task<TableResponse> Handle(DeactivateTableCommand request, CancellationToken cancellationToken)
        {
            return _tableService.DeactivateAsync(request.RestaurantId, request.TableId);
        }
    }
}
