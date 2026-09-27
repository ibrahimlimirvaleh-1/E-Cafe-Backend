namespace ECafe.Application.Services.RefundPayoutDetails.Abstract;

public interface IRefundPayoutDetailsProtector
{
    string Protect(string details);

    string Unprotect(string encryptedDetails);

    string CreateMaskedDetails(string details);
}
