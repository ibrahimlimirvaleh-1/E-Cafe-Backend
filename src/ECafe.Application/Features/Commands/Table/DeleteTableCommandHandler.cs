using ECafe.Application.DTOs.Table;
using ECafe.Application.Services.Table.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Table
{
    public class DeleteTableCommandHandler : IRequestHandler<DeleteTableCommand, TableResponse>
    {
        private readonly ITableService _tableService;

        public DeleteTableCommandHandler(ITableService tableService)
        {
            _tableService = tableService;
        }

        public Task<TableResponse> Handle(DeleteTableCommand request, CancellationToken cancellationToken)
        {
            return _tableService.DeleteAsync(request.RestaurantId, request.TableId);
        }
    }
}
