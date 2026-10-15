using ECafe.Application.DTOs.TableSession;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.TableSessions.Abstract;

public interface ITableSessionCloseReviewService
{
    Task<PaginatedList<WaiterTableSessionResponse>> GetReviewsAsync(int restaurantId, PaginationFilter pagination, CancellationToken cancellationToken = default);
    Task<WaiterTableSessionResponse> ContinueServiceAsync(int restaurantId, int sessionId, ContinueTableServiceRequest request, CancellationToken cancellationToken = default);
    Task<int> NotifyDueReviewsAsync(int batchSize, CancellationToken cancellationToken = default);
}
