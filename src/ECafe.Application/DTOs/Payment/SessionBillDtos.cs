namespace ECafe.Application.DTOs.Payment;

public sealed record SessionBillResponse(int TableSessionId, int RestaurantId, int? ReservationId, string CurrencyCode,
    decimal TotalAmount, decimal DepositCreditAmount, decimal PaidAmount, decimal RemainingAmount,
    bool HasUnservedOrders, bool IsOpen, bool Enabled, bool IsTest, bool CanPay, bool CanClose,
    SessionBillPaymentResponse? LatestPayment);

public sealed record SessionBillPaymentResponse(int Id, int TableSessionId, decimal Amount, string CurrencyCode,
    int StatusId, string Status, bool IsTest, string? CheckoutUrl, DateTimeOffset? PaidAt, bool RequiresRefund);
