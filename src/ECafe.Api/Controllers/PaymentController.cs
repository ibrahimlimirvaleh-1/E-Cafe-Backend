using ECafe.Application.DTOs.Payment;
using ECafe.Application.Features.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PaymentController : BaseController
{
    /// <summary>Ödənişin aktivliyini və test rejimini göstərir.</summary>
    /// <remarks>Test rejimi real pul hərəkəti etmir. Production mühitində simulyasiya bloklanır; rejim yalnız server konfiqurasiyası ilə idarə edilir.</remarks>
    [HttpGet(ApiRoutes.Payment.Capabilities)]
    [ProducesResponseType(typeof(PaymentCapabilities), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCapabilities(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new PaymentCapabilitiesQuery(), cancellationToken));

    /// <summary>Rezervasiyanın rekvizitsiz ödəniş vəziyyətini göstərir.</summary>
    /// <remarks>Yalnız həmin müştəri və restoranın cari menecer/sahibkarı görə bilər. Məbləğ, status, test işarəsi və icazəli növbəti addımlar qaytarılır; kart, hesab və çek məlumatı verilmir.</remarks>
    [HttpGet(ApiRoutes.Payment.Reservation)]
    [ProducesResponseType(typeof(ReservationPaymentSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReservation(int reservationId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new ReservationPaymentQuery(reservationId), cancellationToken));
}
