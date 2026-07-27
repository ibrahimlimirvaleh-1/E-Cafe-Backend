namespace ECafe.Application.Services
{
    public interface IEmailOutboxService
    {
        Task EnqueueContractNotificationAsync(
            string toEmail,
            string toName,
            string subject,
            string body,
            long contractId = 0);
    }
}
