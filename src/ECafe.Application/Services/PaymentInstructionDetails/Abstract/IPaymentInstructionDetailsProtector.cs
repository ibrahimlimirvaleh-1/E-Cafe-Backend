namespace ECafe.Application.Services.PaymentInstructionDetails.Abstract;

public interface IPaymentInstructionDetailsProtector
{
    string Protect(string details);

    string Unprotect(string encryptedDetails);

    string CreateMaskedDetails(string details);
}
