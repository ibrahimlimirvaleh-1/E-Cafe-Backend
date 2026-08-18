using ECafe.Application.DTOs.Outbox;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.Outbox
{
    public class GetOutboxMessagesQuery : OutboxMessageFilterRequest, IRequest<PaginatedList<OutboxMessageResponse>>
    {
    }
}
