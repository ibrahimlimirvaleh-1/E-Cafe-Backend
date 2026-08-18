using ECafe.Application.DTOs.Outbox;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.Outbox.Abstract
{
    public interface IOutboxAdminService
    {
        Task<PaginatedList<OutboxMessageResponse>> GetMessagesAsync(OutboxMessageFilterRequest filter);
        Task<OutboxMessageResponse> GetMessageAsync(Guid id);
        Task<OutboxMessageResponse> RetryAsync(Guid id);
    }
}
