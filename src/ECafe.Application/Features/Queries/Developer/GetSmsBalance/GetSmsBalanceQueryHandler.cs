using ECafe.Application.Services.Sms.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Developer.GetSmsBalance;

public sealed class GetSmsBalanceQueryHandler : IRequestHandler<GetSmsBalanceQuery, SmsBalanceResponse>
{
    private readonly ISmsService _smsService;

    public GetSmsBalanceQueryHandler(ISmsService smsService)
    {
        _smsService = smsService;
    }

    public Task<SmsBalanceResponse> Handle(
        GetSmsBalanceQuery request,
        CancellationToken cancellationToken)
        => _smsService.GetBalanceAsync(cancellationToken);
}
