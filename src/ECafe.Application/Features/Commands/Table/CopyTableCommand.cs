using ECafe.Application.DTOs.Table;
using MediatR;

namespace ECafe.Application.Features.Commands.Table
{
    public class CopyTableCommand : CopyTableRequest, IRequest<List<TableResponse>>
    {
        public int RestaurantId { get; set; }

        public int TableId { get; set; }
    }
}
