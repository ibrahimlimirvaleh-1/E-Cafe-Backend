using ECafe.Application.DTOs.Developer;
using MediatR;

namespace ECafe.Application.Features.Commands.Developer.SendTestEmail;

public sealed class SendTestEmailCommand : IRequest<DeveloperTestNotificationResponse>
{
    public string ToEmail { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Body { get; set; } = null!;
}
