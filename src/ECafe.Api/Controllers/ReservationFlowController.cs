using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Commands.Reservation.ApprovePaymentProof;
using ECafe.Application.Features.Commands.Reservation.Cancel;
using ECafe.Application.Features.Commands.Reservation.CancelRestaurantReservation;
using ECafe.Application.Features.Commands.Reservation.CheckIn;
using ECafe.Application.Features.Commands.Reservation.Complete;
using ECafe.Application.Features.Commands.Reservation.RejectPaymentProof;
using ECafe.Application.Features.Commands.Reservation.SendPaymentInstruction;
using ECafe.Application.Features.Commands.Reservation.SubmitPaymentProof;
using ECafe.Application.Features.Commands.Reservation.WaiveDeposit;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class ReservationFlowController : BaseController
{
    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.ReservationFlow.SubmitPaymentProof)]
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

    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.ReservationFlow.Cancel)]
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

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.ReservationFlow.WaiveDeposit)]
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
    [HttpPost(ApiRoutes.ReservationFlow.SendPaymentInstruction)]
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
    [HttpPost(ApiRoutes.ReservationFlow.ApprovePaymentProof)]
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
    [HttpPost(ApiRoutes.ReservationFlow.RejectPaymentProof)]
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
    [HttpPost(ApiRoutes.ReservationFlow.CheckIn)]
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
    [HttpPost(ApiRoutes.ReservationFlow.Complete)]
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
    [HttpPost(ApiRoutes.ReservationFlow.CancelForRestaurant)]
    public async Task<IActionResult> CancelForRestaurant(
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
}
