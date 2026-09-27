using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.ReservationRefund.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.RequestRefund;

public sealed class RequestReservationRefundCommand : IRequest<ReservationRefundResponse>
{
    public int ReservationId { get; init; }
}

public sealed class RequestReservationRefundCommandHandler
    : IRequestHandler<RequestReservationRefundCommand, ReservationRefundResponse>
{
    private readonly IReservationRefundService _reservationRefundService;

    public RequestReservationRefundCommandHandler(IReservationRefundService reservationRefundService)
    {
        _reservationRefundService = reservationRefundService;
    }

    public Task<ReservationRefundResponse> Handle(
        RequestReservationRefundCommand request,
        CancellationToken cancellationToken)
        => _reservationRefundService.RequestAsync(request.ReservationId, cancellationToken);
}
