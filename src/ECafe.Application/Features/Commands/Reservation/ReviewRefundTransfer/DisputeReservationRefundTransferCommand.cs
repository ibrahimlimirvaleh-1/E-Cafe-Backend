using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.ReservationRefund.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.ReviewRefundTransfer;

public sealed record DisputeReservationRefundTransferCommand(int RefundId, int TransferId, string Reason)
    : IRequest<ReservationRefundResponse>;

public sealed class DisputeReservationRefundTransferCommandHandler
    : IRequestHandler<DisputeReservationRefundTransferCommand, ReservationRefundResponse>
{
    private readonly IReservationRefundService _refundService;

    public DisputeReservationRefundTransferCommandHandler(IReservationRefundService refundService)
    {
        _refundService = refundService;
    }

    public Task<ReservationRefundResponse> Handle(
        DisputeReservationRefundTransferCommand request,
        CancellationToken cancellationToken)
        => _refundService.DisputeTransferAsync(
            request.RefundId,
            request.TransferId,
            request.Reason,
            cancellationToken);
}
