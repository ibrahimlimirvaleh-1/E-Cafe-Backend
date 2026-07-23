using ECafe.Application.DTOs.Table;
using ECafe.Application.Services.Table.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Table
{
    public record GetRestaurantTablesQuery(int RestaurantId) : IRequest<List<TableResponse>>;

    public class GetRestaurantTablesQueryHandler : IRequestHandler<GetRestaurantTablesQuery, List<TableResponse>>
    {
        private readonly ITableService _tableService;

        public GetRestaurantTablesQueryHandler(ITableService tableService)
        {
            _tableService = tableService;
        }

        public Task<List<TableResponse>> Handle(GetRestaurantTablesQuery request, CancellationToken cancellationToken)
            => _tableService.GetByRestaurantAsync(request.RestaurantId);
    }
}
