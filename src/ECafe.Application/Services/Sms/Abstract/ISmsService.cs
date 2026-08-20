namespace ECafe.Application.Services.Sms.Abstract;

public interface ISmsService
{
    Task SendAsync(
        string toPhone,
        string message,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<SmsBalanceResponse> GetBalanceAsync(CancellationToken cancellationToken = default);

    Task<SmsDeliveryStatusResponse> GetStatusAsync(
        string messageId,
        CancellationToken cancellationToken = default);
}
