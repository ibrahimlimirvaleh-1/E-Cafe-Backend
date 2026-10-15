using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.Complete;

public sealed record CompleteOwnReservationCommand(int RestaurantId, int ReservationId) : IRequest<ReservationActionResponse>;
public sealed class CompleteOwnReservationCommandHandler(IReservationService service) : IRequestHandler<CompleteOwnReservationCommand, ReservationActionResponse>
{
    public Task<ReservationActionResponse> Handle(CompleteOwnReservationCommand request, CancellationToken cancellationToken)
        => service.CompleteOwnReservationAsync(request.RestaurantId, request.ReservationId, cancellationToken);
}
