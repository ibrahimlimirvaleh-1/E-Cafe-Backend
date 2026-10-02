using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.Reservation.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize(Roles = "5")]
public sealed class ReservationArrivalFlowController(IReservationArrivalService service) : BaseController
{
    [HttpPost(ApiRoutes.ReservationArrivalFlow.Offer)]
    public async Task<IActionResult> Offer(int reservationId, [FromBody] ReservationArrivalOfferRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.OfferAsync(reservationId, request.ArrivalAt, cancellationToken));

    [HttpPost(ApiRoutes.ReservationArrivalFlow.Accept)]
    public async Task<IActionResult> Accept(int reservationId, [FromBody] AcceptReservationArrivalRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.AcceptAsync(reservationId, request.ConsentToken, cancellationToken));
}
