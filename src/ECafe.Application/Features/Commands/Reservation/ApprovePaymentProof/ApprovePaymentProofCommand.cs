using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Reservation.ApprovePaymentProof;

public sealed class ApprovePaymentProofCommand : IRequest<ReservationActionResponse>
{
    public int RestaurantId { get; init; }
    public int ReservationId { get; init; }
}

public sealed class ApprovePaymentProofCommandHandler
    : IRequestHandler<ApprovePaymentProofCommand, ReservationActionResponse>
{
    private readonly IReservationService _reservationService;

    public ApprovePaymentProofCommandHandler(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public Task<ReservationActionResponse> Handle(
        ApprovePaymentProofCommand request,
        CancellationToken cancellationToken)
        => _reservationService.ApprovePaymentProofAsync(
            request.RestaurantId,
            request.ReservationId,
            cancellationToken);
}
