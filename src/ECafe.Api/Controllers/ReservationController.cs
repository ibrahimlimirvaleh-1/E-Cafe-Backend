using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Commands.Reservation.Cancel;
using ECafe.Application.Features.Commands.Reservation.Create;
using ECafe.Application.Features.Commands.Reservation.RequestRefund;
using ECafe.Application.Features.Commands.Reservation.SubmitRefundPayoutDetails;
using ECafe.Application.Features.Commands.Reservation.SubmitPaymentProof;
using ECafe.Application.Features.Queries.Reservation.GetById;
using ECafe.Application.Features.Queries.Reservation.GetHistory;
using ECafe.Application.Features.Queries.Reservation.GetMy;
using ECafe.Application.Features.Queries.Reservation.GetMyRefund;
using ECafe.Api.Routes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers;

[Authorize(Roles = "5")]
public sealed class ReservationController : BaseController
{
    [HttpGet(ApiRoutes.Reservation.GetMy)]
    public async Task<IActionResult> GetMy(
        [FromQuery] GetMyReservationsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet(ApiRoutes.Reservation.GetById)]
    public async Task<IActionResult> GetById(
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetReservationByIdQuery(reservationId),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet(ApiRoutes.Reservation.GetHistory)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetReservationHistoryQuery(reservationId),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost(ApiRoutes.Reservation.Create)]
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
            Note = request.Note,
            AcceptsLimitedSeating = request.AcceptsLimitedSeating
        }, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost(ApiRoutes.Reservation.SubmitPaymentProof)]
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

    [HttpPost(ApiRoutes.Reservation.Cancel)]
    public async Task<IActionResult> Cancel(
        [FromRoute] int reservationId,
        [FromBody] ReservationCancellationRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CancelReservationCommand
        {
            ReservationId = reservationId,
            Reason = request?.Reason
        }, cancellationToken);

        return Ok(result);
    }

    [HttpGet(ApiRoutes.Reservation.GetRefund)]
    public async Task<IActionResult> GetRefund(
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetMyReservationRefundQuery(reservationId),
            cancellationToken);

        return result is null ? NoContent() : Ok(result);
    }

    [HttpPost(ApiRoutes.Reservation.RequestRefund)]
    public async Task<IActionResult> RequestRefund(
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new RequestReservationRefundCommand { ReservationId = reservationId },
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost(ApiRoutes.Reservation.SubmitRefundPayoutDetails)]
    public async Task<IActionResult> SubmitRefundPayoutDetails(
        [FromRoute] int refundId,
        [FromBody] SubmitReservationRefundPayoutDetailsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new SubmitReservationRefundPayoutDetailsCommand
        {
            RefundId = refundId,
            Details = request.Details
        }, cancellationToken);

        return Ok(result);
    }
}
