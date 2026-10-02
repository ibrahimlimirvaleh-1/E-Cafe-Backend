using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Commands.Reservation.RequestRefund;
using ECafe.Application.Features.Commands.Reservation.ReviewRefundTransfer;
using ECafe.Application.Features.Commands.Reservation.SubmitRefundPayoutDetails;
using ECafe.Application.Features.Commands.Reservation.SubmitRefundTransfer;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class ReservationRefundFlowController : BaseController
{
    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.ReservationRefundFlow.RequestRefund)]
    public async Task<IActionResult> RequestRefund(
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new RequestReservationRefundCommand { ReservationId = reservationId },
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.ReservationRefundFlow.SubmitRefundPayoutDetails)]
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

    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.ReservationRefundFlow.ConfirmRefundTransfer)]
    public async Task<IActionResult> ConfirmRefundTransfer(
        [FromRoute] int refundId,
        [FromBody] ConfirmReservationRefundTransferRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new ConfirmReservationRefundTransferCommand(refundId, request.TransferId),
            cancellationToken);

        return Ok(result);
    }

    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.ReservationRefundFlow.DisputeRefundTransfer)]
    public async Task<IActionResult> DisputeRefundTransfer(
        [FromRoute] int refundId,
        [FromBody] DisputeReservationRefundTransferRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new DisputeReservationRefundTransferCommand(refundId, request.TransferId, request.Reason),
            cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.ReservationRefundFlow.SubmitRefundTransfer)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitRefundTransfer(
        [FromRoute] int restaurantId,
        [FromRoute] int refundId,
        [FromForm] SubmitReservationRefundTransferCommand request,
        CancellationToken cancellationToken)
    {
        request.RestaurantId = restaurantId;
        request.RefundId = refundId;

        var result = await Mediator.Send(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
