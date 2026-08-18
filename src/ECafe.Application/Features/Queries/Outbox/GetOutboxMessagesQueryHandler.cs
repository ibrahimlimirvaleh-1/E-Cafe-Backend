using ECafe.Application.DTOs.Outbox;
using ECafe.Application.Services.Outbox.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.Outbox
{
    public class GetOutboxMessagesQueryHandler
        : IRequestHandler<GetOutboxMessagesQuery, PaginatedList<OutboxMessageResponse>>
    {
        private readonly IOutboxAdminService _outboxAdminService;

        public GetOutboxMessagesQueryHandler(IOutboxAdminService outboxAdminService)
        {
            _outboxAdminService = outboxAdminService;
        }

        public Task<PaginatedList<OutboxMessageResponse>> Handle(
            GetOutboxMessagesQuery request,
            CancellationToken cancellationToken)
            => _outboxAdminService.GetMessagesAsync(request);
    }
}
