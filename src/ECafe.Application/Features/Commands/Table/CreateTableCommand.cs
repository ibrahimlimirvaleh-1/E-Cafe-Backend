using ECafe.Application.DTOs.Table;
using MediatR;

namespace ECafe.Application.Features.Commands.Table
{
    public class CreateTableCommand : CreateTableRequest, IRequest<int>
    {
        public int RestaurantId { get; set; }
    }
}
