using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.Reception;

public sealed record RecordReservationDepartureCommand(int RestaurantId, int ReservationId,
    ReservationDepartureRequest Request) : IRequest<ReservationActionResponse>;

public sealed record ChangeReservationTableCommand(int RestaurantId, int ReservationId,
    ReservationTableChangeRequest Request) : IRequest<ReservationActionResponse>;

public sealed class RecordReservationDepartureCommandHandler(IReservationService service)
    : IRequestHandler<RecordReservationDepartureCommand, ReservationActionResponse>
{
    public Task<ReservationActionResponse> Handle(RecordReservationDepartureCommand request, CancellationToken cancellationToken)
        => service.RecordWaitingDepartureAsync(request.RestaurantId, request.ReservationId, request.Request, cancellationToken);
}

public sealed class ChangeReservationTableCommandHandler(IReservationService service)
    : IRequestHandler<ChangeReservationTableCommand, ReservationActionResponse>
{
    public Task<ReservationActionResponse> Handle(ChangeReservationTableCommand request, CancellationToken cancellationToken)
        => service.ChangeWaitingTableAsync(request.RestaurantId, request.ReservationId, request.Request, cancellationToken);
}
