using ECafe.Application.Services.Sms.Abstract;
using Microsoft.Extensions.Logging;

namespace ECafe.Application.Services.Sms.Concrete;

public sealed class FakeSmsManager : ISmsService
{
    private readonly ILogger<FakeSmsManager> _logger;

    public FakeSmsManager(ILogger<FakeSmsManager> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
        string toPhone,
        string message,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SMS sending is disabled. Message was not sent. Recipient: {Phone}, Length: {Length}, IdempotencyKey: {IdempotencyKey}",
            toPhone,
            message.Length,
            idempotencyKey);

        return Task.CompletedTask;
    }

    public Task<SmsBalanceResponse> GetBalanceAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new SmsBalanceResponse
        {
            Balance = 0,
            Apis = new SmsApiPermissionsResponse
            {
                Otp = true,
                Bulk = true,
                Advertising = false
            },
            Available = new SmsApiPermissionsResponse
            {
                Otp = true,
                Bulk = true,
                Advertising = false
            },
            TestMode = true,
            LimitedSms = true
        });

    public Task<SmsDeliveryStatusResponse> GetStatusAsync(
        string messageId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new SmsDeliveryStatusResponse
        {
            MessageId = messageId.Trim(),
            StatusCode = 2,
            StatusText = "Fake provider delivered",
            Date = DateTime.UtcNow,
            IsFinal = true,
            IsDelivered = true
        });
}
