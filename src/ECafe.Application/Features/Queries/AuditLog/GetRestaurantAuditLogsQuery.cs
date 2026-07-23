using ECafe.Application.DTOs.AuditLog;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.AuditLog
{
    public class GetRestaurantAuditLogsQuery : AuditLogFilterRequest, IRequest<PaginatedList<AuditLogResponse>>
    {
        public int RestaurantId { get; set; }
    }
}
