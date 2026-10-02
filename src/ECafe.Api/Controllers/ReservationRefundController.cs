using ECafe.Application.Features.Queries.Reservation.GetMyRefund;
using ECafe.Application.Features.Queries.Reservation.GetRestaurantRefund;
using ECafe.Application.Features.Queries.Reservation.GetRestaurantRefundPayoutDetails;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class ReservationRefundController : BaseController
{
    [Authorize(Roles = "5")]
    [HttpGet(ApiRoutes.ReservationRefund.GetRefund)]
    public async Task<IActionResult> GetRefund(
        [FromRoute] int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetMyReservationRefundQuery(reservationId),
            cancellationToken);

        return result is null ? NoContent() : Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpGet(ApiRoutes.ReservationRefund.GetRestaurantRefund)]
    public async Task<IActionResult> GetRestaurantRefund(
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
    [HttpGet(ApiRoutes.ReservationRefund.GetRefundPayoutDetails)]
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
}
