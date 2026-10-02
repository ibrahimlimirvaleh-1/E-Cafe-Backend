using ECafe.Application.Services.Reservation.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize(Roles = "5")]
public sealed class ReservationArrivalController(IReservationArrivalService service) : BaseController
{
    [HttpGet(ApiRoutes.ReservationArrival.Get)]
    public async Task<IActionResult> Get(int reservationId, CancellationToken cancellationToken)
        => Ok(await service.GetOptionsAsync(reservationId, cancellationToken));
}
