using ECafe.Application.DTOs.AuditLog;
using ECafe.Shared.DTOs;
using ECafe.Application.Common.Audit;

namespace ECafe.Application.Services.AuditLog.Abstract
{
    public interface IAuditLogService
    {
        Task RecordRestaurantActionAsync(
            int restaurantId,
            string action,
            object? metadata = null,
            string entityType = AuditEntityTypes.Restaurant,
            long? entityId = null,
            string? entityDisplayName = null);

        Task<PaginatedList<AuditLogResponse>> GetRestaurantTimelineAsync(
            int restaurantId,
            AuditLogFilterRequest filter);
    }
}
