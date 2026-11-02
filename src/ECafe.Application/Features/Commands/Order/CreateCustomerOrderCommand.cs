using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.Orders.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Order;

public sealed record CreateCustomerOrderCommand(int TableSessionId, Guid RequestId,
    CreateCustomerOrderRequest Request) : IRequest<CreateCustomerOrderResult>;

public sealed class CreateCustomerOrderCommandHandler(ICustomerOrderSubmissionService service)
    : IRequestHandler<CreateCustomerOrderCommand, CreateCustomerOrderResult>
{
    public Task<CreateCustomerOrderResult> Handle(CreateCustomerOrderCommand request, CancellationToken cancellationToken)
        => service.CreateAsync(request.TableSessionId, request.RequestId, request.Request, cancellationToken);
}
