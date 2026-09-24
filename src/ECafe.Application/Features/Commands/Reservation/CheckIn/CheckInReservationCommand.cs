using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.CheckIn;

public sealed class CheckInReservationCommand : IRequest<ReservationActionResponse>
{
    public int RestaurantId { get; init; }
    public int ReservationId { get; init; }
}

public sealed class CheckInReservationCommandHandler
    : IRequestHandler<CheckInReservationCommand, ReservationActionResponse>
{
    private readonly IReservationService _reservationService;

    public CheckInReservationCommandHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationActionResponse> Handle(
        CheckInReservationCommand request,
        CancellationToken cancellationToken)
        => _reservationService.CheckInReservationAsync(
            request.RestaurantId,
            request.ReservationId,
            cancellationToken);
}
