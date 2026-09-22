using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Commands.Reservation.Create;
using ECafe.Application.Features.Commands.Reservation.SubmitPaymentProof;
using ECafe.Application.Features.Queries.Reservation.GetById;
using ECafe.Application.Features.Queries.Reservation.GetHistory;
using ECafe.Application.Features.Queries.Reservation.GetMy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers;

[Authorize(Roles = "5")]
public sealed class ReservationController : BaseController
{
    [HttpGet("api/v1/public/reservations/my")]
    public async Task<IActionResult> GetMy(
        [FromQuery] GetMyReservationsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("api/v1/public/reservations/{reservationId:int}")]
    public async Task<IActionResult> GetById(
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetReservationByIdQuery(reservationId),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("api/v1/public/reservations/{reservationId:int}/history")]
    public async Task<IActionResult> GetHistory(
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetReservationHistoryQuery(reservationId),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("api/v1/restaurants/{restaurantId:int}/reservations")]
    public async Task<IActionResult> Create(
        [FromRoute] int restaurantId,
        [FromBody] CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateReservationCommand
        {
            RestaurantId = restaurantId,
            TableId = request.TableId,
            ReservedAt = request.ReservedAt,
            PeopleCount = request.PeopleCount,
            Note = request.Note
        }, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("api/v1/restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-proofs")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitPaymentProof(
        [FromRoute] int restaurantId,
        [FromRoute] int reservationId,
        [FromForm] SubmitPaymentProofCommand request,
        CancellationToken cancellationToken)
    {
        request.RestaurantId = restaurantId;
        request.ReservationId = reservationId;

        var result = await Mediator.Send(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
