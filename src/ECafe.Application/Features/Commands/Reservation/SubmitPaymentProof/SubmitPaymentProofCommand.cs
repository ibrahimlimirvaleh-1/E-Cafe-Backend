using ECafe.Application.DTOs.Reservation;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.Features.Commands.Reservation.SubmitPaymentProof;

public sealed class SubmitPaymentProofCommand : IRequest<PaymentProofResponse>
{
    public int RestaurantId { get; set; }
    public int ReservationId { get; set; }
    public IFormFile? File { get; set; }
}
