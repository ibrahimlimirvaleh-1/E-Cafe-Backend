using ECafe.Application.DTOs.Table;
using MediatR;

namespace ECafe.Application.Features.Commands.Table
{
    public record DeactivateTableCommand(int RestaurantId, int TableId) : IRequest<TableResponse>;
}
