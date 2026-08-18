using ECafe.Application.DTOs.Outbox;
using MediatR;

namespace ECafe.Application.Features.Queries.Outbox
{
    public class GetOutboxMessageByIdQuery : IRequest<OutboxMessageResponse>
    {
        public Guid Id { get; set; }

        public GetOutboxMessageByIdQuery(Guid id)
        {
            Id = id;
        }
    }
}
