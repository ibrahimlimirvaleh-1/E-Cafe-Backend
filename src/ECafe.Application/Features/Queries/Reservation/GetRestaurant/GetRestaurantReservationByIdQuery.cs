using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Reservation.GetRestaurant;

public sealed class GetRestaurantReservationByIdQuery : IRequest<ReservationResponse>
{
    public int RestaurantId { get; init; }
    public int ReservationId { get; init; }
}

public sealed class GetRestaurantReservationByIdQueryHandler
    : IRequestHandler<GetRestaurantReservationByIdQuery, ReservationResponse>
{
    private readonly IReservationService _reservationService;

    public GetRestaurantReservationByIdQueryHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationResponse> Handle(
        GetRestaurantReservationByIdQuery request,
        CancellationToken cancellationToken)
        => _reservationService.GetRestaurantReservationByIdAsync(
            request.RestaurantId,
            request.ReservationId,
            cancellationToken);
}
