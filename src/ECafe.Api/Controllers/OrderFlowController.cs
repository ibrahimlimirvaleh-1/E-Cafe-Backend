using ECafe.Application.DTOs.Order;
using ECafe.Application.Features.Commands.Order;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
[HasPermission(PermissionCode.ManageOrders)]
public sealed class OrderFlowController : BaseController
{
    /// <summary>Hazırlanan sifariş üçün məsul ofisiantın səbəbli ləğv sorğusunu mətbəxə göndərir; hələ sifarişi ləğv etmir.</summary>
    [HttpPost(ApiRoutes.OrderFlow.RequestCancellation)]
    [ProducesResponseType(typeof(SessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestCancellation([FromRoute] int restaurantId, [FromRoute] int tableSessionId,
        [FromRoute] int orderId, [FromBody] CancelOrderRequest request, CancellationToken token)
        => Ok(await Mediator.Send(new RequestWaiterCancellationCommand(restaurantId, tableSessionId, orderId, request), token));

    /// <summary>Müştərinin ləğv istəyini cari məsul ofisiant dəyişməz səbəb ilə mətbəxə ötürür.</summary>
    [HttpPost(ApiRoutes.OrderFlow.ForwardCancellation)]
    [ProducesResponseType(typeof(SessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForwardCancellation([FromRoute] int restaurantId, [FromRoute] int tableSessionId,
        [FromRoute] int orderId, CancellationToken token)
        => Ok(await Mediator.Send(new ForwardCancellationCommand(restaurantId, tableSessionId, orderId), token));

    /// <summary>Cari məsul ofisiantın açıq sessiyasındakı hazırlanmamış sifarişi səbəb ilə ləğv edir.</summary>
    /// <remarks>Yalnız Created/Accepted və faktiki hazırlama vaxtı olmayan sifariş. Body: reason (3-500 simvol). Təkrar eyni orderId ilk ləğv vaxtını/səbəbini qaytarır. Hazırlama, stok sərfiyyatı və aktiv ödəniş olduqda 409. Stok, depozit, rezervasiya və sessiya dəyişmir. Mövcud xidmət müqavilə bitdikdən sonra da təhlükəsiz ləğv edilə bilər.</remarks>
    [HttpPost(ApiRoutes.OrderFlow.Cancel)]
    [ProducesResponseType(typeof(SessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel([FromRoute] int restaurantId, [FromRoute] int tableSessionId,
        [FromRoute] int orderId, [FromBody] CancelOrderRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new CancelWaiterOrderCommand(restaurantId, tableSessionId, orderId, request), cancellationToken));

    /// <summary>Ofisiant öz xidmət etdiyi masanın hazır sifarişini müştəriyə təqdim edilmiş kimi qeyd edir.</summary>
    /// <remarks>POST, body və yeni Idempotency-Key yoxdur. Yalnız bazada aktiv ofisiant və öz açıq sessiyası üçün Ready(2004) → Served(2005). ServedAt UTC ilə bir dəfə yazılır, əməkdaş auditdə saxlanılır. Eyni orderId təkrarı 200 və ilk vaxtı qaytarır; ikinci audit yaratmır. Sifarişin ilkin yaradıcısı başqa ofisiant ola bilər. Başqa restoran/sessiya/sifariş 404, uyğun olmayan status 409. Mövcud xidmət müqavilə bitdikdə də tamamlana bilər. Stok, ödəniş, rezervasiya və masa sessiyası dəyişmir; Served ödənilib demək deyil.</remarks>
    [HttpPost(ApiRoutes.OrderFlow.MarkServed)]
    [ProducesResponseType(typeof(SessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkServed([FromRoute] int restaurantId, [FromRoute] int tableSessionId,
        [FromRoute] int orderId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new MarkOrderServedCommand(restaurantId, tableSessionId, orderId), cancellationToken));

    /// <summary>Ofisiant öz açıq masa sessiyasındakı sifarişi mətbəx növbəsinə göndərir.</summary>
    /// <remarks>Body və yeni Idempotency-Key lazım deyil. Eyni orderId-ni təkrar göndərmək ikinci göndəriş, audit və bildiriş yaratmır. SentToKitchenAt UTC ilə doldurulur, Created statusu mətbəxin qəbulunu gözləyir. Stok, ödəniş və rezervasiya dəyişmir. Başqa ofisiantın sifarişi 404, uyğun olmayan status 409 qaytarır.</remarks>
    [RequireActiveRestaurantContract]
    [HttpPost(ApiRoutes.OrderFlow.SendToKitchen)]
    [ProducesResponseType(typeof(SessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendToKitchen([FromRoute] int restaurantId, [FromRoute] int tableSessionId,
        [FromRoute] int orderId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SendOrderToKitchenCommand(restaurantId, tableSessionId, orderId), cancellationToken));
}
