using ECafe.Application.DTOs.Reservation;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.SendPaymentInstruction;

public sealed class SendPaymentInstructionCommand : PaymentInstructionRequest, IRequest<PaymentInstructionResponse>
{
    public int RestaurantId { get; set; }

    public int ReservationId { get; set; }
}
