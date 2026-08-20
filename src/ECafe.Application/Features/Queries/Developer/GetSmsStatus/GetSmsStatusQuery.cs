using ECafe.Application.Services.Sms.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Developer.GetSmsStatus;

public sealed class GetSmsStatusQuery : IRequest<SmsDeliveryStatusResponse>
{
    public string MessageId { get; set; } = null!;
}
