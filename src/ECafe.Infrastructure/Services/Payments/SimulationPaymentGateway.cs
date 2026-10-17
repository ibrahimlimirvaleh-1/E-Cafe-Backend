using ECafe.Application.DTOs.Payment;
using ECafe.Application.Services.Payments.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;

namespace ECafe.Infrastructure.Services.Payments;

public sealed class SimulationPaymentGateway : IPaymentGateway
{
    public string Name => "simulation";
    public bool IsTest => true;

    /// <summary>Kart qəbul etmədən davamlı test əməliyyatı yaradır; təkrar çağırış mövcud istinadı dəyişmir.</summary>
    public Task CreateCheckoutAsync(Payment payment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        payment.ProviderRef ??= $"sim_pay_{Guid.NewGuid():N}";
        payment.ProviderStatus = ProviderOperationStatus.Pending;
        return Task.CompletedTask;
    }

    /// <summary>Yalnız test endpointinin saxladığı test nəticəsini oxuyur; canlı adapter burada provayder API-sini serverdən yoxlamalıdır.</summary>
    public Task<GatewayPaymentResult> VerifyPaymentAsync(Payment payment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new GatewayPaymentResult(payment.ProviderRef!, payment.Amount, payment.CurrencyCode,
            payment.IsTest, payment.ProviderStatus));
    }

    /// <summary>İlkin ödənişə bağlı refundu sabit ID ilə başladır; yeni kart və hesab rekviziti qəbul etmir.</summary>
    public Task CreateRefundAsync(Payment payment, ReservationRefund refund, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        refund.ProviderRef ??= $"sim_refund_{refund.Id}";
        refund.ProviderStatus = ProviderOperationStatus.Pending;
        return Task.CompletedTask;
    }

    /// <summary>Refund test nəticəsini ilkin ödənişin məbləğ və rejim məlumatı ilə qaytarır; heç bir bank köçürməsi etmir.</summary>
    public Task<GatewayPaymentResult> VerifyRefundAsync(Payment payment, ReservationRefund refund, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new GatewayPaymentResult(refund.ProviderRef!, refund.Amount, refund.CurrencyCode,
            payment.IsTest, refund.ProviderStatus));
    }
}
