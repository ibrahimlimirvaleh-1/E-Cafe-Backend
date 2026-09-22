using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Commands.Reservation.ApprovePaymentProof;
using ECafe.Application.Features.Commands.Reservation.CancelRestaurantReservation;
using ECafe.Application.Features.Commands.Reservation.RejectPaymentProof;
using ECafe.Application.Features.Commands.Reservation.SendPaymentInstruction;
using ECafe.Application.Features.Queries.Reservation.GetRestaurant;
using ECafe.Application.Features.Queries.Reservation.GetRestaurantHistory;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class RestaurantReservationController : BaseController
{
    [HasPermission(PermissionCode.ManageReservations)]
    [HttpGet("api/v1/restaurants/{restaurantId:int}/reservations")]
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
    [HttpGet("api/v1/restaurants/{restaurantId:int}/reservations/{reservationId:int}")]
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
    [HttpGet("api/v1/restaurants/{restaurantId:int}/reservations/{reservationId:int}/history")]
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
    [HttpPost("api/v1/restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-instructions")]
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
    [HttpPost("api/v1/restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-proofs/approve")]
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
    [HttpPost("api/v1/restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-proofs/reject")]
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
    [HttpPost("api/v1/restaurants/{restaurantId:int}/reservations/{reservationId:int}/cancel")]
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
}
