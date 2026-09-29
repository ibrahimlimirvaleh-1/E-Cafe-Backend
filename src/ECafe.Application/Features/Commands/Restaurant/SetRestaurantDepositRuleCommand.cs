using MediatR;

namespace ECafe.Application.Features.Commands.Restaurant;

public sealed record SetRestaurantDepositRuleCommand(int RestaurantId, DateOnly ReservationDate, decimal Amount) : IRequest;
