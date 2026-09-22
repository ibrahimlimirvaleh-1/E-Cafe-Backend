using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.RejectPaymentProof;

public sealed class RejectPaymentProofCommand : IRequest<ReservationActionResponse>
{
    public int RestaurantId { get; init; }
    public int ReservationId { get; init; }
    public string Reason { get; init; } = null!;
}

public sealed class RejectPaymentProofCommandHandler
    : IRequestHandler<RejectPaymentProofCommand, ReservationActionResponse>
{
    private readonly IReservationService _reservationService;

    public RejectPaymentProofCommandHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationActionResponse> Handle(
        RejectPaymentProofCommand request,
        CancellationToken cancellationToken)
        => _reservationService.RejectPaymentProofAsync(
            request.RestaurantId,
            request.ReservationId,
            request.Reason,
            cancellationToken);
}
