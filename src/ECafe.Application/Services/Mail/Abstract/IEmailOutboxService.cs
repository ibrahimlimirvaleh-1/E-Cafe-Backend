namespace ECafe.Application.Services
{
    public interface IEmailOutboxService
    {
        Task EnqueueEmailAsync(
            string toEmail,
            string toName,
            string subject,
            string body,
            string aggregateType,
            long aggregateId,
            string? relatedEntityType = null,
            long? relatedEntityId = null);

        Task EnqueueContractNotificationAsync(
            string toEmail,
            string toName,
            string subject,
            string body,
            long contractId = 0);
    }
}
