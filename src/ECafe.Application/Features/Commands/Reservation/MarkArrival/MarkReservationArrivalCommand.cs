using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.MarkArrival;

public sealed class MarkReservationArrivalCommand : IRequest<ReservationActionResponse>
{
    public int RestaurantId { get; init; }
    public int ReservationId { get; init; }
}

public sealed class MarkReservationArrivalCommandHandler
    : IRequestHandler<MarkReservationArrivalCommand, ReservationActionResponse>
{
    private readonly IReservationService _reservationService;

    public MarkReservationArrivalCommandHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationActionResponse> Handle(
        MarkReservationArrivalCommand request,
        CancellationToken cancellationToken)
        => _reservationService.MarkArrivalAsync(
            request.RestaurantId,
            request.ReservationId,
            cancellationToken);
}
