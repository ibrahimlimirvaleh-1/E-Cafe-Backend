using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Reservation.GetRestaurantHistory;

public sealed class GetRestaurantReservationHistoryQuery : IRequest<ReservationHistoryResponse>
{
    public int RestaurantId { get; init; }
    public int ReservationId { get; init; }
}

public sealed class GetRestaurantReservationHistoryQueryHandler
    : IRequestHandler<GetRestaurantReservationHistoryQuery, ReservationHistoryResponse>
{
    private readonly IReservationService _reservationService;

    public GetRestaurantReservationHistoryQueryHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationHistoryResponse> Handle(
        GetRestaurantReservationHistoryQuery request,
        CancellationToken cancellationToken)
        => _reservationService.GetRestaurantReservationHistoryAsync(
            request.RestaurantId,
            request.ReservationId,
            cancellationToken);
}
