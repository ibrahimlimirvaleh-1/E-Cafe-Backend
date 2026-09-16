using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Commands.Reservation.Create;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers;

[Authorize(Roles = "5")]
public sealed class ReservationController : BaseController
{
    [HttpPost("api/v1/restaurants/{restaurantId}/reservations")]
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
}
