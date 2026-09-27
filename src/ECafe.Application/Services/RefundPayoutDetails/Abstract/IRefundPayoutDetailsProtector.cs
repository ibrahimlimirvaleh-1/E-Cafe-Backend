namespace ECafe.Application.Services.RefundPayoutDetails.Abstract;

public interface IRefundPayoutDetailsProtector
{
    string Protect(string details);

    string CreateMaskedDetails(string details);
}
