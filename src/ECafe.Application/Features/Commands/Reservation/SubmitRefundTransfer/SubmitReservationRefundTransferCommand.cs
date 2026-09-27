using ECafe.Application.DTOs.Reservation;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.Features.Commands.Reservation.SubmitRefundTransfer;

public sealed class SubmitReservationRefundTransferCommand : IRequest<ReservationRefundTransferResponse>
{
    public int RestaurantId { get; set; }
    public int RefundId { get; set; }
    public string? TransferReference { get; set; }
    public IFormFile? ProofFile { get; set; }
}
