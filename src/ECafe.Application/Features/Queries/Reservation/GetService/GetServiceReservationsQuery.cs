using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.Reservation.GetService;

public sealed record GetServiceReservationsQuery(int RestaurantId, RestaurantReservationsQueryRequest Filters)
    : IRequest<PaginatedList<ReservationServiceItemResponse>>;

public sealed class GetServiceReservationsQueryHandler
    : IRequestHandler<GetServiceReservationsQuery, PaginatedList<ReservationServiceItemResponse>>
{
    private readonly IReservationService _reservationService;

    public GetServiceReservationsQueryHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<PaginatedList<ReservationServiceItemResponse>> Handle(
        GetServiceReservationsQuery request,
        CancellationToken cancellationToken)
        => _reservationService.GetServiceReservationsAsync(
            request.RestaurantId, request.Filters, cancellationToken);
}
