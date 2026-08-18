using ECafe.Application.DTOs.Outbox;
using ECafe.Application.Services.Outbox.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Outbox
{
    public class GetOutboxMessageByIdQueryHandler
        : IRequestHandler<GetOutboxMessageByIdQuery, OutboxMessageResponse>
    {
        private readonly IOutboxAdminService _outboxAdminService;

        public GetOutboxMessageByIdQueryHandler(IOutboxAdminService outboxAdminService)
        {
            _outboxAdminService = outboxAdminService;
        }

        public Task<OutboxMessageResponse> Handle(
            GetOutboxMessageByIdQuery request,
            CancellationToken cancellationToken)
            => _outboxAdminService.GetMessageAsync(request.Id);
    }
}
