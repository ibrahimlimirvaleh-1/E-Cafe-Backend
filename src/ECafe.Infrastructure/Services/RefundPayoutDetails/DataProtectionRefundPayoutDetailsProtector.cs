using ECafe.Application.Services.RefundPayoutDetails.Abstract;
using Microsoft.AspNetCore.DataProtection;

namespace ECafe.Infrastructure.Services.RefundPayoutDetails;

public sealed class DataProtectionRefundPayoutDetailsProtector : IRefundPayoutDetailsProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionRefundPayoutDetailsProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("ECafe.ReservationRefund.PayoutDetails.v1");
    }

    public string Protect(string details) => _protector.Protect(details);

    public string CreateMaskedDetails(string details)
    {
        var visibleCharacters = details
            .Where(char.IsLetterOrDigit)
            .TakeLast(4)
            .ToArray();

        return visibleCharacters.Length == 0
            ? "Gizli odenis melumati"
            : $"**** {new string(visibleCharacters)}";
    }
}
