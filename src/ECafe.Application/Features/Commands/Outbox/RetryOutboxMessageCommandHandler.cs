using ECafe.Application.DTOs.Outbox;
using ECafe.Application.Services.Outbox.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Outbox
{
    public class RetryOutboxMessageCommandHandler
        : IRequestHandler<RetryOutboxMessageCommand, OutboxMessageResponse>
    {
        private readonly IOutboxAdminService _outboxAdminService;

        public RetryOutboxMessageCommandHandler(IOutboxAdminService outboxAdminService)
        {
            _outboxAdminService = outboxAdminService;
        }

        public Task<OutboxMessageResponse> Handle(
            RetryOutboxMessageCommand request,
            CancellationToken cancellationToken)
            => _outboxAdminService.RetryAsync(request.Id);
    }
}
