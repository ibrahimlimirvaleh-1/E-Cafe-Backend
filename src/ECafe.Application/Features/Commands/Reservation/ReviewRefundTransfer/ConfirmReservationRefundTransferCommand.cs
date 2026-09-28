using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.ReservationRefund.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.ReviewRefundTransfer;

public sealed record ConfirmReservationRefundTransferCommand(int RefundId, int TransferId)
    : IRequest<ReservationRefundResponse>;

public sealed class ConfirmReservationRefundTransferCommandHandler
    : IRequestHandler<ConfirmReservationRefundTransferCommand, ReservationRefundResponse>
{
    private readonly IReservationRefundService _refundService;

    public ConfirmReservationRefundTransferCommandHandler(IReservationRefundService refundService)
    {
        _refundService = refundService;
    }

    public Task<ReservationRefundResponse> Handle(
        ConfirmReservationRefundTransferCommand request,
        CancellationToken cancellationToken)
        => _refundService.ConfirmTransferAsync(request.RefundId, request.TransferId, cancellationToken);
}
