using ECafe.Application.DTOs.Reservation;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.Create;

public sealed class CreateReservationCommand : CreateReservationRequest, IRequest<ReservationResponse>
{
    public int RestaurantId { get; set; }
}
