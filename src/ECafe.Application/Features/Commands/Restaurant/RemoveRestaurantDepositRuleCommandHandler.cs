using ECafe.Application.Services.Restaurant.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Restaurant;

public sealed class RemoveRestaurantDepositRuleCommandHandler(IRestaurantDepositService depositService)
    : IRequestHandler<RemoveRestaurantDepositRuleCommand>
{
    public Task Handle(RemoveRestaurantDepositRuleCommand request, CancellationToken cancellationToken)
        => depositService.RemoveRuleAsync(request.RestaurantId, request.ReservationDate);
}
