using System.Text.Json.Serialization;
using ECafe.Domain.Enums;

namespace ECafe.Application.DTOs.Payment;

public sealed record PaymentCapabilities(bool Enabled, bool IsTest, string Mode, string Message);
public sealed record ReservationPaymentSummary(int ReservationId, bool Enabled, bool IsTest, bool CanAuthorize,
    bool CanPay, CheckoutPaymentResponse? LatestPayment);
public sealed record CheckoutPaymentResponse(int Id, int ReservationId, decimal Amount, string CurrencyCode,
    int StatusId, string Status, bool IsTest, string? CheckoutUrl, DateTimeOffset? ExpiresAt,
    DateTimeOffset? PaidAt, DateTimeOffset? RefundedAt, bool RequiresRefund);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SimulatePaymentRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter<ProviderOperationStatus>))]
    [JsonRequired]
    public ProviderOperationStatus Outcome { get; init; }
}

public sealed record GatewayPaymentResult(string Reference, decimal Amount, string CurrencyCode,
    bool IsTest, ProviderOperationStatus Status);
