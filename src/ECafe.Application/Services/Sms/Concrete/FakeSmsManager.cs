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
}
