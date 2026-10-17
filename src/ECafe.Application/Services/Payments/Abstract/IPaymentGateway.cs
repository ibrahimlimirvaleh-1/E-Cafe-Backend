using ECafe.Application.DTOs.Payment;
using ECafe.Domain.Entities;
using RefundEntity = ECafe.Domain.Entities.ReservationRefund;

namespace ECafe.Application.Services.Payments.Abstract;

public interface IPaymentGateway
{
    string Name { get; }
    bool IsTest { get; }
    Task CreateCheckoutAsync(Payment payment, CancellationToken cancellationToken);
    Task<GatewayPaymentResult> VerifyPaymentAsync(Payment payment, CancellationToken cancellationToken);
    Task CreateRefundAsync(Payment payment, RefundEntity refund, CancellationToken cancellationToken);
    Task<GatewayPaymentResult> VerifyRefundAsync(Payment payment, RefundEntity refund, CancellationToken cancellationToken);
}
