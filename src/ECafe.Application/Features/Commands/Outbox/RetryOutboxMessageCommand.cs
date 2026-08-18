using ECafe.Application.DTOs.Outbox;
using MediatR;

namespace ECafe.Application.Features.Commands.Outbox
{
    public class RetryOutboxMessageCommand : IRequest<OutboxMessageResponse>
    {
        public Guid Id { get; set; }

        public RetryOutboxMessageCommand(Guid id)
        {
            Id = id;
        }
    }
}
