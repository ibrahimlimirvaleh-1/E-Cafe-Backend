using ECafe.Application.DTOs.Reservation;
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

    [HttpPost(ApiRoutes.ReservationArrival.Offer)]
    public async Task<IActionResult> Offer(int reservationId, [FromBody] ReservationArrivalOfferRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.OfferAsync(reservationId, request.ArrivalAt, cancellationToken));

    [HttpPost(ApiRoutes.ReservationArrival.Accept)]
    public async Task<IActionResult> Accept(int reservationId, [FromBody] AcceptReservationArrivalRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.AcceptAsync(reservationId, request.ConsentToken, cancellationToken));
}
