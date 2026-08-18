using ECafe.Application.DTOs.Table;
using MediatR;

namespace ECafe.Application.Features.Commands.Table
{
    public class UpdateTableCommand : UpdateTableRequest, IRequest<TableResponse>
    {
        public int RestaurantId { get; set; }

        public int TableId { get; set; }
    }
}
