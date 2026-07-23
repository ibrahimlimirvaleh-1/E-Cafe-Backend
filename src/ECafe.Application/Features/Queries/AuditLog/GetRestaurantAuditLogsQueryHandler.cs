using ECafe.Application.DTOs.AuditLog;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.AuditLog
{
    public class GetRestaurantAuditLogsQueryHandler
        : IRequestHandler<GetRestaurantAuditLogsQuery, PaginatedList<AuditLogResponse>>
    {
        private readonly IAuditLogService _auditLogService;

        public GetRestaurantAuditLogsQueryHandler(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        public Task<PaginatedList<AuditLogResponse>> Handle(
            GetRestaurantAuditLogsQuery request,
            CancellationToken cancellationToken)
            => _auditLogService.GetRestaurantTimelineAsync(request.RestaurantId, request);
    }
}
