using ECafe.Application.Services.Sms.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Developer.GetSmsBalance;

public sealed class GetSmsBalanceQuery : IRequest<SmsBalanceResponse>
{
}
