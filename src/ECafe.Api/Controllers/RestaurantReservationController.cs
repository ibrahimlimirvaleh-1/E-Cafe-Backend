using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Commands.Reservation.ApprovePaymentProof;
using ECafe.Application.Features.Commands.Reservation.CancelRestaurantReservation;
using ECafe.Application.Features.Commands.Reservation.CheckIn;
using ECafe.Application.Features.Commands.Reservation.Complete;
using ECafe.Application.Features.Commands.Reservation.RejectPaymentProof;
using ECafe.Application.Features.Commands.Reservation.SendPaymentInstruction;
using ECafe.Application.Features.Commands.Reservation.SubmitRefundTransfer;
using ECafe.Application.Features.Commands.Reservation.WaiveDeposit;
using ECafe.Application.Features.Queries.Reservation.GetRestaurant;
using ECafe.Application.Features.Queries.Reservation.GetRestaurantHistory;
using ECafe.Application.Features.Queries.Reservation.GetRestaurantRefund;
using ECafe.Application.Features.Queries.Reservation.GetRestaurantRefundPayoutDetails;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class RestaurantReservationController : BaseController
{
    [HasPermission(PermissionCode.ManageReservations)]
    [HttpGet(ApiRoutes.RestaurantReservation.GetList)]
    public async Task<IActionResult> GetList(
        [FromRoute] int restaurantId,
        [FromQuery] GetRestaurantReservationsQuery request,
        CancellationToken cancellationToken)
    {
        request.RestaurantId = restaurantId;
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpGet(ApiRoutes.RestaurantReservation.GetById)]
    public async Task<IActionResult> GetById(
        [FromRoute] int restaurantId,
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRestaurantReservationByIdQuery
        {
            RestaurantId = restaurantId,
            ReservationId = reservationId
        }, cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpGet(ApiRoutes.RestaurantReservation.GetHistory)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] int restaurantId,
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRestaurantReservationHistoryQuery
        {
            RestaurantId = restaurantId,
            ReservationId = reservationId
        }, cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.RestaurantReservation.WaiveDeposit)]
    public async Task<IActionResult> WaiveDeposit(
        [FromRoute] int restaurantId,
        [FromRoute] int reservationId,
        [FromBody] ReservationCancellationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new WaiveDepositCommand
        {
            RestaurantId = restaurantId,
            ReservationId = reservationId,
            Reason = request?.Reason ?? string.Empty
        }, cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.RestaurantReservation.SendPaymentInstruction)]
    public async Task<IActionResult> SendPaymentInstruction(
        [FromRoute] int restaurantId,
        [FromRoute] int reservationId,
        [FromBody] SendPaymentInstructionCommand request,
        CancellationToken cancellationToken)
    {
        request.RestaurantId = restaurantId;
        request.ReservationId = reservationId;

        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.RestaurantReservation.ApprovePaymentProof)]
    public async Task<IActionResult> ApprovePaymentProof(
        [FromRoute] int restaurantId,
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ApprovePaymentProofCommand
        {
            RestaurantId = restaurantId,
            ReservationId = reservationId
        }, cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.RestaurantReservation.RejectPaymentProof)]
    public async Task<IActionResult> RejectPaymentProof(
        [FromRoute] int restaurantId,
        [FromRoute] int reservationId,
        [FromBody] ReservationCancellationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RejectPaymentProofCommand
        {
            RestaurantId = restaurantId,
            ReservationId = reservationId,
            Reason = request?.Reason ?? string.Empty
        }, cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.RestaurantReservation.CheckIn)]
    public async Task<IActionResult> CheckIn(
        [FromRoute] int restaurantId,
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CheckInReservationCommand
        {
            RestaurantId = restaurantId,
            ReservationId = reservationId
        }, cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.RestaurantReservation.Complete)]
    public async Task<IActionResult> Complete(
        [FromRoute] int restaurantId,
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CompleteReservationCommand
        {
            RestaurantId = restaurantId,
            ReservationId = reservationId
        }, cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.RestaurantReservation.Cancel)]
    public async Task<IActionResult> Cancel(
        [FromRoute] int restaurantId,
        [FromRoute] int reservationId,
        [FromBody] ReservationCancellationRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CancelRestaurantReservationCommand
        {
            RestaurantId = restaurantId,
            ReservationId = reservationId,
            Reason = request?.Reason
        }, cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpGet(ApiRoutes.RestaurantReservation.GetRefund)]
    public async Task<IActionResult> GetRefund(
        [FromRoute] int restaurantId,
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRestaurantReservationRefundQuery
        {
            RestaurantId = restaurantId,
            ReservationId = reservationId
        }, cancellationToken);

        return result is null ? NoContent() : Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpGet(ApiRoutes.RestaurantReservation.GetRefundPayoutDetails)]
    public async Task<IActionResult> GetRefundPayoutDetails(
        [FromRoute] int restaurantId,
        [FromRoute] int refundId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRestaurantReservationRefundPayoutDetailsQuery
        {
            RestaurantId = restaurantId,
            RefundId = refundId
        }, cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.RestaurantReservation.SubmitRefundTransfer)]
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
