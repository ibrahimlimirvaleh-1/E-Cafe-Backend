using ECafe.Application.Services.PaymentInstructionDetails.Abstract;
using Microsoft.AspNetCore.DataProtection;

namespace ECafe.Infrastructure.Services.PaymentInstructionDetails;

public sealed class DataProtectionPaymentInstructionDetailsProtector
    : IPaymentInstructionDetailsProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionPaymentInstructionDetailsProtector(
        IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(
            "ECafe.Reservation.PaymentInstruction.Details.v1");
    }

    public string Protect(string details) => _protector.Protect(details);

    public string Unprotect(string encryptedDetails) => _protector.Unprotect(encryptedDetails);

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
