using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.CancelRestaurantReservation;

public sealed class CancelRestaurantReservationCommand : IRequest<ReservationActionResponse>
{
    public int RestaurantId { get; init; }
    public int ReservationId { get; init; }
    public string? Reason { get; init; }
}

public sealed class CancelRestaurantReservationCommandHandler
    : IRequestHandler<CancelRestaurantReservationCommand, ReservationActionResponse>
{
    private readonly IReservationService _reservationService;

    public CancelRestaurantReservationCommandHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationActionResponse> Handle(
        CancelRestaurantReservationCommand request,
        CancellationToken cancellationToken)
        => _reservationService.CancelRestaurantReservationAsync(
            request.RestaurantId,
            request.ReservationId,
            request.Reason,
            cancellationToken);
}
