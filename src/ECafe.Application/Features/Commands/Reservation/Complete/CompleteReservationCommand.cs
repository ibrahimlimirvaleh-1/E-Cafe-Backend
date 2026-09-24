using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.Complete;

public sealed class CompleteReservationCommand : IRequest<ReservationActionResponse>
{
    public int RestaurantId { get; init; }
    public int ReservationId { get; init; }
}

public sealed class CompleteReservationCommandHandler
    : IRequestHandler<CompleteReservationCommand, ReservationActionResponse>
{
    private readonly IReservationService _reservationService;

    public CompleteReservationCommandHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationActionResponse> Handle(
        CompleteReservationCommand request,
        CancellationToken cancellationToken)
        => _reservationService.CompleteReservationAsync(
            request.RestaurantId,
            request.ReservationId,
            cancellationToken);
}
