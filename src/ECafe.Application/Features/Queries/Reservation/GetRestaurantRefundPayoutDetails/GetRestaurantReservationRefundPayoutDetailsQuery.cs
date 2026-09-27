using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.ReservationRefund.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Reservation.GetRestaurantRefundPayoutDetails;

public sealed class GetRestaurantReservationRefundPayoutDetailsQuery
    : IRequest<RestaurantReservationRefundPayoutDetailsResponse>
{
    public int RestaurantId { get; init; }
    public int RefundId { get; init; }
}

public sealed class GetRestaurantReservationRefundPayoutDetailsQueryHandler
    : IRequestHandler<
        GetRestaurantReservationRefundPayoutDetailsQuery,
        RestaurantReservationRefundPayoutDetailsResponse>
{
    private readonly IReservationRefundService _reservationRefundService;

    public GetRestaurantReservationRefundPayoutDetailsQueryHandler(
        IReservationRefundService reservationRefundService)
    {
        _reservationRefundService = reservationRefundService;
    }

    public Task<RestaurantReservationRefundPayoutDetailsResponse> Handle(
        GetRestaurantReservationRefundPayoutDetailsQuery request,
        CancellationToken cancellationToken)
        => _reservationRefundService.GetPayoutDetailsForRestaurantAsync(
            request.RestaurantId,
            request.RefundId,
            cancellationToken);
}
