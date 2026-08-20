using ECafe.Application.Services.Sms.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Developer.GetSmsStatus;

public sealed class GetSmsStatusQueryHandler : IRequestHandler<GetSmsStatusQuery, SmsDeliveryStatusResponse>
{
    private readonly ISmsService _smsService;

    public GetSmsStatusQueryHandler(ISmsService smsService)
    {
        _smsService = smsService;
    }

    public Task<SmsDeliveryStatusResponse> Handle(
        GetSmsStatusQuery request,
        CancellationToken cancellationToken)
        => _smsService.GetStatusAsync(request.MessageId, cancellationToken);
}
