using ECafe.Application.DTOs.Payment;
using ECafe.Application.Services.Payments.Abstract;
using ECafe.Domain.Enums;
using MediatR;

namespace ECafe.Application.Features.Payments;

public sealed record SessionBillQuery(int TableSessionId) : IRequest<SessionBillResponse>;
public sealed record SessionBillCheckoutCommand(int TableSessionId, string Key) : IRequest<SessionBillPaymentResponse>;
public sealed record SessionBillRefreshCommand(int TableSessionId, int PaymentId) : IRequest<SessionBillPaymentResponse>;
public sealed record SessionBillSimulateCommand(int TableSessionId, int PaymentId, ProviderOperationStatus Outcome) : IRequest<SessionBillPaymentResponse>;

public sealed class SessionBillHandlers(ITableSessionBillingService service) :
    IRequestHandler<SessionBillQuery, SessionBillResponse>,
    IRequestHandler<SessionBillCheckoutCommand, SessionBillPaymentResponse>,
    IRequestHandler<SessionBillRefreshCommand, SessionBillPaymentResponse>,
    IRequestHandler<SessionBillSimulateCommand, SessionBillPaymentResponse>
{
    public Task<SessionBillResponse> Handle(SessionBillQuery request, CancellationToken cancellationToken)
        => service.GetAsync(request.TableSessionId, cancellationToken);
    public Task<SessionBillPaymentResponse> Handle(SessionBillCheckoutCommand request, CancellationToken cancellationToken)
        => service.CheckoutAsync(request.TableSessionId, request.Key, cancellationToken);
    public Task<SessionBillPaymentResponse> Handle(SessionBillRefreshCommand request, CancellationToken cancellationToken)
        => service.RefreshAsync(request.TableSessionId, request.PaymentId, cancellationToken);
    public Task<SessionBillPaymentResponse> Handle(SessionBillSimulateCommand request, CancellationToken cancellationToken)
        => service.SimulateAsync(request.TableSessionId, request.PaymentId, request.Outcome, cancellationToken);
}
