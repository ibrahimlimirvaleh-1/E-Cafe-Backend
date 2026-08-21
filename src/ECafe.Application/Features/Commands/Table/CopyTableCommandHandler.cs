using ECafe.Application.DTOs.Table;
using ECafe.Application.Services.Table.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Table
{
    public class CopyTableCommandHandler : IRequestHandler<CopyTableCommand, List<TableResponse>>
    {
        private readonly ITableService _tableService;

        public CopyTableCommandHandler(ITableService tableService)
        {
            _tableService = tableService;
        }

        public Task<List<TableResponse>> Handle(CopyTableCommand request, CancellationToken cancellationToken)
            => _tableService.CopyTableAsync(request.RestaurantId, request.TableId, request);
    }
}
