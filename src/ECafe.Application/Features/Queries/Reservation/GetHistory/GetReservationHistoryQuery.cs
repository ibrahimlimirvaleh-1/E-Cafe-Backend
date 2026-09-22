using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Reservation.GetHistory;

public sealed class GetReservationHistoryQuery : IRequest<ReservationHistoryResponse>
{
    public int ReservationId { get; init; }

    public GetReservationHistoryQuery(int reservationId)
    {
        ReservationId = reservationId;
    }
}

public sealed class GetReservationHistoryQueryHandler
    : IRequestHandler<GetReservationHistoryQuery, ReservationHistoryResponse>
{
    private readonly IReservationService _reservationService;

    public GetReservationHistoryQueryHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationHistoryResponse> Handle(
        GetReservationHistoryQuery request,
        CancellationToken cancellationToken)
        => _reservationService.GetReservationHistoryAsync(
            request.ReservationId,
            cancellationToken);
}
