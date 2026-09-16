using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.Create;

public sealed class CreateReservationCommandHandler : IRequestHandler<CreateReservationCommand, ReservationResponse>
{
    private readonly IReservationService _reservationService;

    public CreateReservationCommandHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationResponse> Handle(
        CreateReservationCommand request,
        CancellationToken cancellationToken)
        => _reservationService.CreateReservationAsync(
            request.RestaurantId,
            request,
            cancellationToken);
}
