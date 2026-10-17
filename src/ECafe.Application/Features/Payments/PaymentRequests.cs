using ECafe.Application.DTOs.Payment;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Payments.Abstract;
using ECafe.Domain.Enums;
using MediatR;

namespace ECafe.Application.Features.Payments;

public sealed record PaymentCapabilitiesQuery : IRequest<PaymentCapabilities>;
public sealed record ReservationPaymentQuery(int ReservationId) : IRequest<ReservationPaymentSummary>;
public sealed record AuthorizeDepositCommand(int RestaurantId, int ReservationId) : IRequest<ReservationPaymentSummary>;
public sealed record CreatePaymentCheckoutCommand(int ReservationId, string Key) : IRequest<CheckoutPaymentResponse>;
public sealed record RefreshPaymentCommand(int PaymentId) : IRequest<CheckoutPaymentResponse>;
public sealed record SimulatePaymentCommand(int PaymentId, ProviderOperationStatus Outcome) : IRequest<CheckoutPaymentResponse>;
public sealed record SimulateRefundCommand(int RefundId, ProviderOperationStatus Outcome) : IRequest<ReservationRefundResponse>;
public sealed record RefreshRefundCommand(int RefundId) : IRequest<ReservationRefundResponse>;

public sealed class PaymentRequestHandlers(IProviderPaymentService service) :
    IRequestHandler<PaymentCapabilitiesQuery, PaymentCapabilities>,
    IRequestHandler<ReservationPaymentQuery, ReservationPaymentSummary>,
    IRequestHandler<AuthorizeDepositCommand, ReservationPaymentSummary>,
    IRequestHandler<CreatePaymentCheckoutCommand, CheckoutPaymentResponse>,
    IRequestHandler<RefreshPaymentCommand, CheckoutPaymentResponse>,
    IRequestHandler<SimulatePaymentCommand, CheckoutPaymentResponse>,
    IRequestHandler<SimulateRefundCommand, ReservationRefundResponse>,
    IRequestHandler<RefreshRefundCommand, ReservationRefundResponse>
{
    public Task<PaymentCapabilities> Handle(PaymentCapabilitiesQuery request, CancellationToken cancellationToken)
        => Task.FromResult(service.GetCapabilities());
    public Task<ReservationPaymentSummary> Handle(ReservationPaymentQuery request, CancellationToken cancellationToken)
        => service.GetReservationAsync(request.ReservationId, cancellationToken);
    public Task<ReservationPaymentSummary> Handle(AuthorizeDepositCommand request, CancellationToken cancellationToken)
        => service.AuthorizeDepositAsync(request.RestaurantId, request.ReservationId, cancellationToken);
    public Task<CheckoutPaymentResponse> Handle(CreatePaymentCheckoutCommand request, CancellationToken cancellationToken)
        => service.CreateCheckoutAsync(request.ReservationId, request.Key, cancellationToken);
    public Task<CheckoutPaymentResponse> Handle(RefreshPaymentCommand request, CancellationToken cancellationToken)
        => service.RefreshAsync(request.PaymentId, cancellationToken);
    public Task<CheckoutPaymentResponse> Handle(SimulatePaymentCommand request, CancellationToken cancellationToken)
        => service.SimulateAsync(request.PaymentId, request.Outcome, cancellationToken);
    public Task<ReservationRefundResponse> Handle(SimulateRefundCommand request, CancellationToken cancellationToken)
        => service.SimulateRefundAsync(request.RefundId, request.Outcome, cancellationToken);
    public Task<ReservationRefundResponse> Handle(RefreshRefundCommand request, CancellationToken cancellationToken)
        => service.RefreshRefundAsync(request.RefundId, cancellationToken);
}
