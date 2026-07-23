using ECafe.Application.DTOs.AuditLog;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.AuditLog.Abstract
{
    public interface IAuditLogService
    {
        Task RecordRestaurantActionAsync(
            int restaurantId,
            string action,
            object? metadata = null,
            string entityType = "Restaurant",
            long? entityId = null,
            string? entityDisplayName = null);

        Task<PaginatedList<AuditLogResponse>> GetRestaurantTimelineAsync(
            int restaurantId,
            AuditLogFilterRequest filter);
    }
}
