using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.Reservation.GetMy;

public sealed class GetMyReservationsQuery : ReservationQueryRequest, IRequest<PaginatedList<ReservationResponse>>
{
}

public sealed class GetMyReservationsQueryHandler
    : IRequestHandler<GetMyReservationsQuery, PaginatedList<ReservationResponse>>
{
    private readonly IReservationService _reservationService;

    public GetMyReservationsQueryHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<PaginatedList<ReservationResponse>> Handle(
        GetMyReservationsQuery request,
        CancellationToken cancellationToken)
        => _reservationService.GetMyReservationsAsync(request, cancellationToken);
}
