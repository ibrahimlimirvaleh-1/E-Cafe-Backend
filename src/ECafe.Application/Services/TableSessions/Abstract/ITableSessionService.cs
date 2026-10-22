using ECafe.Application.DTOs.TableSession;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.TableSessions.Abstract;

public interface ITableSessionService
{
    Task<PaginatedList<WaiterReadyOrderResponse>> GetMyReadyOrdersAsync(
        int restaurantId, PaginationFilter pagination, CancellationToken cancellationToken = default);

    Task<PaginatedList<WaiterTableSessionResponse>> GetMyOpenSessionsAsync(
        int restaurantId, PaginationFilter pagination, CancellationToken cancellationToken = default);

    Task<WaiterTableSessionResponse> GetMyOpenSessionAsync(
        int restaurantId, int tableSessionId, CancellationToken cancellationToken = default);
}
