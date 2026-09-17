using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Reservation.GetById;

public sealed class GetReservationByIdQuery : IRequest<ReservationResponse>
{
    public int ReservationId { get; init; }

    public GetReservationByIdQuery(int reservationId)
    {
        ReservationId = reservationId;
    }
}

public sealed class GetReservationByIdQueryHandler : IRequestHandler<GetReservationByIdQuery, ReservationResponse>
{
    private readonly IReservationService _reservationService;

    public GetReservationByIdQueryHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationResponse> Handle(
        GetReservationByIdQuery request,
        CancellationToken cancellationToken)
        => _reservationService.GetReservationByIdAsync(request.ReservationId, cancellationToken);
}
