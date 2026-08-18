using ECafe.Application.DTOs.Table;
using MediatR;

namespace ECafe.Application.Features.Commands.Table
{
    public record DeleteTableCommand(int RestaurantId, int TableId) : IRequest<TableResponse>;
}
