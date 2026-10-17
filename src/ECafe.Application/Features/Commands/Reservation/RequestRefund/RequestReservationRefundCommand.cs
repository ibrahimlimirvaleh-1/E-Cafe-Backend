using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Payments.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.RequestRefund;

public sealed class RequestReservationRefundCommand : IRequest<ReservationRefundResponse>
{
    public int ReservationId { get; init; }
}

public sealed class RequestReservationRefundCommandHandler
    : IRequestHandler<RequestReservationRefundCommand, ReservationRefundResponse>
{
    private readonly IProviderPaymentService _reservationRefundService;

    public RequestReservationRefundCommandHandler(IProviderPaymentService reservationRefundService)
    {
        _reservationRefundService = reservationRefundService;
    }

    public Task<ReservationRefundResponse> Handle(
        RequestReservationRefundCommand request,
        CancellationToken cancellationToken)
        => _reservationRefundService.RequestRefundAsync(request.ReservationId, cancellationToken);
}
