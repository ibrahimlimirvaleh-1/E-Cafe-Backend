using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.WaiveDeposit;

public sealed class WaiveDepositCommand : IRequest<ReservationActionResponse>
{
    public int RestaurantId { get; set; }
    public int ReservationId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class WaiveDepositCommandHandler(IReservationService reservationService)
    : IRequestHandler<WaiveDepositCommand, ReservationActionResponse>
{
    public Task<ReservationActionResponse> Handle(
        WaiveDepositCommand request,
        CancellationToken cancellationToken)
        => reservationService.WaiveDepositAsync(
            request.RestaurantId,
            request.ReservationId,
            request.Reason,
            cancellationToken);
}
