using ECafe.Application.DTOs.Developer;
using ECafe.Application.Services;
using MediatR;

namespace ECafe.Application.Features.Commands.Developer.SendTestEmail;

public sealed class SendTestEmailCommandHandler : IRequestHandler<SendTestEmailCommand, DeveloperTestNotificationResponse>
{
    private readonly IEmailService _emailService;

    public SendTestEmailCommandHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task<DeveloperTestNotificationResponse> Handle(
        SendTestEmailCommand request,
        CancellationToken cancellationToken)
    {
        var recipient = request.ToEmail.Trim();
        await _emailService.SendAsync(
            recipient,
            request.Subject.Trim(),
            request.Body.Trim(),
            cancellationToken);

        return new DeveloperTestNotificationResponse
        {
            Sent = true,
            Channel = "Email",
            Recipient = recipient,
            SentAt = DateTime.UtcNow
        };
    }
}
