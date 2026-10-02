using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Commands.Reservation.Create;
using ECafe.Application.Features.Queries.Reservation.GetById;
using ECafe.Application.Features.Queries.Reservation.GetHistory;
using ECafe.Application.Features.Queries.Reservation.GetMy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

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
}
