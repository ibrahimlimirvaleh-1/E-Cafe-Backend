using ECafe.Application.Services.Restaurant.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Restaurant;

public sealed class SetRestaurantDepositRuleCommandHandler(IRestaurantDepositService depositService)
    : IRequestHandler<SetRestaurantDepositRuleCommand>
{
    public Task Handle(SetRestaurantDepositRuleCommand request, CancellationToken cancellationToken)
        => depositService.SetRuleAsync(request.RestaurantId, request.ReservationDate, request.Amount);
}
