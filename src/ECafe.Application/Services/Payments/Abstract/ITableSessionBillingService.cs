using ECafe.Application.DTOs.Payment;
using ECafe.Domain.Enums;

namespace ECafe.Application.Services.Payments.Abstract;

public interface ITableSessionBillingService
{
    Task<SessionBillResponse> GetAsync(int tableSessionId, CancellationToken cancellationToken);
    Task<SessionBillPaymentResponse> CheckoutAsync(int tableSessionId, string key, CancellationToken cancellationToken);
    Task<SessionBillPaymentResponse> RefreshAsync(int tableSessionId, int paymentId, CancellationToken cancellationToken);
    Task<SessionBillPaymentResponse> SimulateAsync(int tableSessionId, int paymentId, ProviderOperationStatus outcome, CancellationToken cancellationToken);
}
