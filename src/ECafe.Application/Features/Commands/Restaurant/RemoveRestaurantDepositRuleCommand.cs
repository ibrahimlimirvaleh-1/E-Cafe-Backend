using MediatR;

namespace ECafe.Application.Features.Commands.Restaurant;

public sealed record RemoveRestaurantDepositRuleCommand(int RestaurantId, DateOnly ReservationDate) : IRequest;
