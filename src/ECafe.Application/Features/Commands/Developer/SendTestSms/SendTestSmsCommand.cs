using ECafe.Application.DTOs.Developer;
using MediatR;

namespace ECafe.Application.Features.Commands.Developer.SendTestSms;

public sealed class SendTestSmsCommand : IRequest<DeveloperTestNotificationResponse>
{
    public string ToPhone { get; set; } = null!;

    public string Message { get; set; } = null!;
}
