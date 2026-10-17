using ECafe.Application.DTOs.Payment;
using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Enums;

namespace ECafe.Application.Services.Payments.Abstract;

public interface IProviderPaymentService
{
    PaymentCapabilities GetCapabilities();
    Task<ReservationPaymentSummary> GetReservationAsync(int reservationId, CancellationToken cancellationToken);
    Task<ReservationPaymentSummary> AuthorizeDepositAsync(int restaurantId, int reservationId, CancellationToken cancellationToken);
    Task<CheckoutPaymentResponse> CreateCheckoutAsync(int reservationId, string key, CancellationToken cancellationToken);
    Task<CheckoutPaymentResponse> RefreshAsync(int paymentId, CancellationToken cancellationToken);
    Task<CheckoutPaymentResponse> SimulateAsync(int paymentId, ProviderOperationStatus outcome, CancellationToken cancellationToken);
    Task<ReservationRefundResponse> RequestRefundAsync(int reservationId, CancellationToken cancellationToken);
    Task<ReservationRefundResponse> RefreshRefundAsync(int refundId, CancellationToken cancellationToken);
    Task<ReservationRefundResponse> SimulateRefundAsync(int refundId, ProviderOperationStatus outcome, CancellationToken cancellationToken);
}
