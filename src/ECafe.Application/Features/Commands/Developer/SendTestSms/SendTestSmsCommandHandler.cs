using ECafe.Application.Common.Validation;
using ECafe.Application.DTOs.Developer;
using ECafe.Application.Services.Sms.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Developer.SendTestSms;

public sealed class SendTestSmsCommandHandler : IRequestHandler<SendTestSmsCommand, DeveloperTestNotificationResponse>
{
    private readonly ISmsService _smsService;

    public SendTestSmsCommandHandler(ISmsService smsService)
    {
        _smsService = smsService;
    }

    public async Task<DeveloperTestNotificationResponse> Handle(
        SendTestSmsCommand request,
        CancellationToken cancellationToken)
    {
        var recipient = PhoneNumberValidationExtensions.NormalizeAzerbaijanPhoneNumber(request.ToPhone);
        await _smsService.SendAsync(
            recipient,
            request.Message.Trim(),
            cancellationToken: cancellationToken);

        return new DeveloperTestNotificationResponse
        {
            Sent = true,
            Channel = "Sms",
            Recipient = recipient,
            SentAt = DateTime.UtcNow
        };
    }
}
