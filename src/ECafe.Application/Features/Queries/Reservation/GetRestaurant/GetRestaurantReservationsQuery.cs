using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.Reservation.GetRestaurant;

public sealed record GetRestaurantReservationsQuery(int RestaurantId, RestaurantReservationsQueryRequest Filters)
    : IRequest<PaginatedList<ReservationResponse>>;

public sealed class GetRestaurantReservationsQueryHandler
    : IRequestHandler<GetRestaurantReservationsQuery, PaginatedList<ReservationResponse>>
{
    private readonly IReservationService _reservationService;

    public GetRestaurantReservationsQueryHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<PaginatedList<ReservationResponse>> Handle(
        GetRestaurantReservationsQuery request,
        CancellationToken cancellationToken)
        => _reservationService.GetRestaurantReservationsAsync(request.RestaurantId, request.Filters, cancellationToken);
}
