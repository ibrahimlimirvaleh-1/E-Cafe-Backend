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
    /// <summary>Ofisiant öz açıq masa sessiyasındakı sifarişi mətbəx növbəsinə göndərir.</summary>
    /// <remarks>Body və yeni Idempotency-Key lazım deyil. Eyni orderId-ni təkrar göndərmək ikinci göndəriş, audit və bildiriş yaratmır. SentToKitchenAt UTC ilə doldurulur, Created statusu mətbəxin qəbulunu gözləyir. Stok, ödəniş və rezervasiya dəyişmir. Başqa ofisiantın sifarişi 404, uyğun olmayan status 409 qaytarır.</remarks>
    [RequireActiveRestaurantContract]
    [HttpPost(ApiRoutes.OrderFlow.SendToKitchen)]
    [ProducesResponseType(typeof(SessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendToKitchen([FromRoute] int restaurantId, [FromRoute] int tableSessionId,
        [FromRoute] int orderId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SendOrderToKitchenCommand(restaurantId, tableSessionId, orderId), cancellationToken));
}
