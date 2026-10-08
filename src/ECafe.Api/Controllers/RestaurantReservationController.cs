using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Queries.Reservation.GetRestaurant;
using ECafe.Application.Features.Queries.Reservation.GetRestaurantHistory;
using ECafe.Application.Features.Queries.Reservation.GetService;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class RestaurantReservationController : BaseController
{
    [HasPermission(PermissionCode.RecordReservationArrival)]
    [HttpGet(ApiRoutes.RestaurantReservation.GetService)]
    public async Task<IActionResult> GetService(
        [FromRoute] int restaurantId,
        [FromQuery] RestaurantReservationsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetServiceReservationsQuery(restaurantId, request), cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCode.ManageReservations)]
    [HttpGet(ApiRoutes.RestaurantReservation.GetList)]
    public async Task<IActionResult> GetList(
        [FromRoute] int restaurantId,
        [FromQuery] RestaurantReservationsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRestaurantReservationsQuery(restaurantId, request), cancellationToken);
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
}
