using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.ReservationRefund.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.SubmitRefundPayoutDetails;

public sealed class SubmitReservationRefundPayoutDetailsCommand
    : SubmitReservationRefundPayoutDetailsRequest, IRequest<ReservationRefundResponse>
{
    public int RefundId { get; init; }
}

public sealed class SubmitReservationRefundPayoutDetailsCommandHandler
    : IRequestHandler<SubmitReservationRefundPayoutDetailsCommand, ReservationRefundResponse>
{
    private readonly IReservationRefundService _reservationRefundService;

    public SubmitReservationRefundPayoutDetailsCommandHandler(
        IReservationRefundService reservationRefundService)
    {
        _reservationRefundService = reservationRefundService;
    }

    public Task<ReservationRefundResponse> Handle(
        SubmitReservationRefundPayoutDetailsCommand request,
        CancellationToken cancellationToken)
        => _reservationRefundService.SubmitPayoutDetailsAsync(
            request.RefundId,
            request.Details,
            cancellationToken);
}
