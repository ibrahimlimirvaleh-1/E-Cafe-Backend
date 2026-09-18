using ECafe.Application.Features.Commands.Reservation.SendPaymentInstruction;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class RestaurantReservationController : BaseController
{
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
}
