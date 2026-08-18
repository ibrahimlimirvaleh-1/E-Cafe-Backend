using ECafe.Application.DTOs.Table;
using ECafe.Application.Services.Table.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Table
{
    public class UpdateTableCommandHandler : IRequestHandler<UpdateTableCommand, TableResponse>
    {
        private readonly ITableService _tableService;

        public UpdateTableCommandHandler(ITableService tableService)
        {
            _tableService = tableService;
        }

        public Task<TableResponse> Handle(UpdateTableCommand request, CancellationToken cancellationToken)
        {
            return _tableService.UpdateAsync(request.RestaurantId, request.TableId, request);
        }
    }
}
