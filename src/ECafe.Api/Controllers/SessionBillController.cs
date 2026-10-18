using ECafe.Application.DTOs.Payment;
using ECafe.Application.Features.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SessionBillController : BaseController
{
    /// <summary>Masa sessiyasının yekun hesabını, qalan borcu və icazəli əməliyyatları serverdən gətirir.</summary>
    /// <remarks>Öz müştərisi, cari məsul ofisiant və restoranın aktiv menecer/sahibkarı görə bilər. Ləğv edilmiş sifarişlər cəmə daxil deyil. Kart, rekvizit və çek məlumatı saxlanmır.</remarks>
    [HttpGet(ApiRoutes.Payment.SessionBill)]
    [ProducesResponseType(typeof(SessionBillResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(int tableSessionId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SessionBillQuery(tableSessionId), cancellationToken));

    /// <summary>Təqdim edilmiş sifarişlərin qalan hesabına rekvizitsiz test ödənişi başladır.</summary>
    /// <remarks>Idempotency-Key boş olmayan UUID olmalıdır. Məbləğ və valyuta body ilə qəbul edilmir; server hesablayır. Gözləmədə olan ödəniş zamanı yeni sifariş və sessiyanın bağlanması bloklanır. Real pul hərəkəti yoxdur.</remarks>
    [HttpPost(ApiRoutes.Payment.SessionBillCheckout)]
    [ProducesResponseType(typeof(SessionBillPaymentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Checkout(int tableSessionId, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SessionBillCheckoutCommand(tableSessionId, key), cancellationToken));

    /// <summary>Yekun hesab ödənişini provayder adapterindən yoxlayır; browserin uğur səhifəsi ödəniş sübutu deyil.</summary>
    [HttpPost(ApiRoutes.Payment.SessionBillRefresh)]
    [ProducesResponseType(typeof(SessionBillPaymentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(int tableSessionId, int paymentId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SessionBillRefreshCommand(tableSessionId, paymentId), cancellationToken));

    /// <summary>Aktiv simulyasiya rejimində həmin masa hesabının test nəticəsini dəyişir.</summary>
    /// <remarks>Outcome: Pending, Succeeded, Failed, Cancelled. IsTest=false əməliyyat qəbul olunmur. Təkrar uğur borcu ikinci dəfə azaltmır və sessiyanı avtomatik bağlamır.</remarks>
    [HttpPost(ApiRoutes.Payment.SessionBillTestOutcome)]
    [ProducesResponseType(typeof(SessionBillPaymentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Simulate(int tableSessionId, int paymentId, [FromBody] SimulatePaymentRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SessionBillSimulateCommand(tableSessionId, paymentId, request.Outcome), cancellationToken));
}
