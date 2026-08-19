namespace ECafe.Application.Services.Sms.Abstract;

public interface ISmsService
{
    Task SendAsync(
        string toPhone,
        string message,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}
