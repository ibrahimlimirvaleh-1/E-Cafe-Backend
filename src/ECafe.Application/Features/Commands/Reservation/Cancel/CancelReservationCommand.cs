using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.Cancel;

public sealed class CancelReservationCommand : IRequest<ReservationActionResponse>
{
    public int ReservationId { get; init; }
    public string? Reason { get; init; }
}

public sealed class CancelReservationCommandHandler
    : IRequestHandler<CancelReservationCommand, ReservationActionResponse>
{
    private readonly IReservationService _reservationService;

    public CancelReservationCommandHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationActionResponse> Handle(
        CancelReservationCommand request,
        CancellationToken cancellationToken)
        => _reservationService.CancelReservationAsync(
            request.ReservationId,
            request.Reason,
            cancellationToken);
}
