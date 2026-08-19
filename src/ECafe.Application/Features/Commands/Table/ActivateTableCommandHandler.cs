using ECafe.Application.DTOs.Table;
using ECafe.Application.Services.Table.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Table
{
    public class ActivateTableCommandHandler : IRequestHandler<ActivateTableCommand, TableResponse>
    {
        private readonly ITableService _tableService;

        public ActivateTableCommandHandler(ITableService tableService)
        {
            _tableService = tableService;
        }

        public Task<TableResponse> Handle(ActivateTableCommand request, CancellationToken cancellationToken)
        {
            return _tableService.ActivateAsync(request.RestaurantId, request.TableId);
        }
    }
}
