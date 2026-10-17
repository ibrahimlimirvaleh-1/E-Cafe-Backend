using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Payment;
using ECafe.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace ECafe.Application.Services.Payments.Concrete;

public sealed class PaymentRuntime(IOptions<PaymentOptions> options, PaymentEnvironment environment)
{
    public bool SimulationEnabled => options.Value.EnableSimulation &&
        string.Equals(options.Value.Mode, "Simulation", StringComparison.OrdinalIgnoreCase) &&
        (environment.Name is "Development" or "Staging" or "Testing");

    /// <summary>UI-yə yalnız serverdə həqiqətən icazə verilmiş rejimi göstərir; Production-da test aktiv görünmür.</summary>
    public PaymentCapabilities GetCapabilities() => new(SimulationEnabled, SimulationEnabled,
        SimulationEnabled ? "Simulation" : "Disabled", SimulationEnabled ? PaymentTexts.TestNotice : PaymentTexts.Unavailable);

    /// <summary>Konfiqurasiya və mühit uyğun deyilsə həm ödənişi, həm refund simulyasiyasını serverdə dayandırır.</summary>
    public void EnsureEnabled()
    {
        if (!SimulationEnabled) throw new BusinessRuleException(ErrorCode.PaymentsUnavailable);
    }

    /// <summary>Test nəticəsinin yalnız test əməliyyatına tətbiqini təmin edir; real və köhnə ödənişə toxunmur.</summary>
    public void EnsureSimulation(bool isTest)
    {
        if (!SimulationEnabled || !isTest) throw new BusinessRuleException(ErrorCode.PaymentSimulationUnavailable);
    }
}
