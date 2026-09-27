using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.ReservationRefund.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Reservation.GetMyRefund;

public sealed class GetMyReservationRefundQuery : IRequest<ReservationRefundResponse?>
{
    public GetMyReservationRefundQuery(int reservationId)
    {
        ReservationId = reservationId;
    }

    public int ReservationId { get; }
}

public sealed class GetMyReservationRefundQueryHandler
    : IRequestHandler<GetMyReservationRefundQuery, ReservationRefundResponse?>
{
    private readonly IReservationRefundService _reservationRefundService;

    public GetMyReservationRefundQueryHandler(IReservationRefundService reservationRefundService)
    {
        _reservationRefundService = reservationRefundService;
    }

    public Task<ReservationRefundResponse?> Handle(
        GetMyReservationRefundQuery request,
        CancellationToken cancellationToken)
        => _reservationRefundService.GetMyByReservationAsync(
            request.ReservationId,
            cancellationToken);
}
