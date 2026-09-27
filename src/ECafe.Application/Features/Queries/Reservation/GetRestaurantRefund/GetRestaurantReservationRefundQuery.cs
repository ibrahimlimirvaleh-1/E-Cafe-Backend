using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.ReservationRefund.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Reservation.GetRestaurantRefund;

public sealed class GetRestaurantReservationRefundQuery : IRequest<ReservationRefundResponse?>
{
    public int RestaurantId { get; init; }
    public int ReservationId { get; init; }
}

public sealed class GetRestaurantReservationRefundQueryHandler
    : IRequestHandler<GetRestaurantReservationRefundQuery, ReservationRefundResponse?>
{
    private readonly IReservationRefundService _reservationRefundService;

    public GetRestaurantReservationRefundQueryHandler(IReservationRefundService reservationRefundService)
    {
        _reservationRefundService = reservationRefundService;
    }

    public Task<ReservationRefundResponse?> Handle(
        GetRestaurantReservationRefundQuery request,
        CancellationToken cancellationToken)
        => _reservationRefundService.GetRestaurantByReservationAsync(
            request.RestaurantId,
            request.ReservationId,
            cancellationToken);
}
