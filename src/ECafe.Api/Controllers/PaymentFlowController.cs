using ECafe.Application.DTOs.Payment;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Payments;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PaymentFlowController : BaseController
{
    /// <summary>Restoran rezervasiyanın depozit ödənişinə icazə verir.</summary>
    /// <remarks>Menecer/sahibkar restoran cavab müddətində qərar verir. Əvvəlki depozit və ödəniş gözləmə müddətləri saxlanılır. Body və bank rekviziti göndərilmir. Təkrar sorğu müddəti uzatmır.</remarks>
    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.Payment.Authorize)]
    [ProducesResponseType(typeof(ReservationPaymentSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> AuthorizeDeposit(int restaurantId, int reservationId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new AuthorizeDepositCommand(restaurantId, reservationId), cancellationToken));

    /// <summary>Müştəri depozit üçün provayder ödəniş sessiyası yaradır.</summary>
    /// <remarks>Idempotency-Key UUID formatında məcburidir: eyni cəhdin təkrarında eyni açarı göndərin. Məbləğ serverdə rezervasiyadan götürülür. Kart, CVV, PIN, IBAN və çek body-si yoxdur. Test rejimində checkoutUrl null ola bilər.</remarks>
    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.Payment.Checkout)]
    [ProducesResponseType(typeof(CheckoutPaymentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCheckout(int reservationId, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new CreatePaymentCheckoutCommand(reservationId, key), cancellationToken));

    /// <summary>Ödəniş nəticəsini provayderdən server üzərindən yoxlayır.</summary>
    /// <remarks>Browserdə uğur səhifəsinin açılması ödəniş sübutu deyil. Təkrar yoxlama ikinci ödəniş, təsdiq və bildiriş yaratmır. Vaxtından gec təsdiq rezervasiyanı bərpa etmir; ilkin ödənişə refund başladılır.</remarks>
    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.Payment.Refresh)]
    [ProducesResponseType(typeof(CheckoutPaymentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(int paymentId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new RefreshPaymentCommand(paymentId), cancellationToken));

    /// <summary>Yalnız test mühitində öz ödənişinin nəticəsini simulyasiya edir.</summary>
    /// <remarks>Outcome: Pending, Succeeded, Failed, Cancelled. Real pul tutulmur. Payments:Mode=Simulation, EnableSimulation=true və Development/Staging/Testing mühiti birlikdə tələb olunur. Production-da rədd edilir.</remarks>
    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.Payment.TestOutcome)]
    [ProducesResponseType(typeof(CheckoutPaymentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Simulate(int paymentId, [FromBody] SimulatePaymentRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SimulatePaymentCommand(paymentId, request.Outcome), cancellationToken));

    /// <summary>Yalnız test əməliyyatına bağlı refundun nəticəsini simulyasiya edir.</summary>
    /// <remarks>Outcome: Pending, Succeeded, Failed. Yeni kart/hesab və ya çek göndərilmir. Köhnə əl ilə ödənmiş depozit test provayderi ilə geri qaytarılmış sayıla bilməz. Real pul hərəkəti yoxdur.</remarks>
    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.Payment.RefundTestOutcome)]
    [ProducesResponseType(typeof(ReservationRefundResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SimulateRefund(int refundId, [FromBody] SimulatePaymentRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SimulateRefundCommand(refundId, request.Outcome), cancellationToken));

    /// <summary>Geri ödənişin nəticəsini serverdən provayderdə yoxlayır.</summary>
    /// <remarks>Yalnız öz refunduna giriş verilir. Rekvizit və ya müştərinin “aldım” təsdiqi tələb olunmur; provayder uğuru təsdiqləmədən məbləğ qaytarılmış sayılmır.</remarks>
    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.Payment.RefundRefresh)]
    [ProducesResponseType(typeof(ReservationRefundResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshRefund(int refundId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new RefreshRefundCommand(refundId), cancellationToken));
}
