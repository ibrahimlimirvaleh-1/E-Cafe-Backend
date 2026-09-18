using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.SendPaymentInstruction;

public sealed class SendPaymentInstructionCommandHandler
    : IRequestHandler<SendPaymentInstructionCommand, PaymentInstructionResponse>
{
    private readonly IReservationService _reservationService;

    public SendPaymentInstructionCommandHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<PaymentInstructionResponse> Handle(
        SendPaymentInstructionCommand request,
        CancellationToken cancellationToken)
        => _reservationService.SendPaymentInstructionAsync(
            request.RestaurantId,
            request.ReservationId,
            request,
            cancellationToken);
}
