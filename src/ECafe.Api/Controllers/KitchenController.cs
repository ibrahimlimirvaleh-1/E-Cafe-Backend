using ECafe.Application.DTOs.Order;
using ECafe.Application.Features.Queries.Order;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
[HasPermission(PermissionCode.ManageKitchenOrders)]
public sealed class KitchenController : BaseController
{
    /// <summary>Restoranın aktiv mətbəx işçisinə göndərilmiş sifarişlərin real növbəsini qaytarır.</summary>
    /// <remarks>Yalnız göndərilmiş Created, Accepted, Preparing və Ready sifarişlər göstərilir. Əvvəl göndərilən əvvəl gəlir; bərabərlikdə ID ilə sıralanır. Göndərilməmiş, təqdim edilmiş, bağlanmış, ləğv edilmiş və planlaşdırılmış sifarişlər daxil deyil. Tarix filtri yoxdur: gecədən qalan sifariş gizlənmir. Müştəri, qiymət və ödəniş məlumatı qaytarılmır. Bu endpoint qəbul/hazırlama statuslarını dəyişmir.</remarks>
    /// <param name="statusId">İstəyə bağlı mərhələ: 2001, 2002, 2003 və ya 2004. Boş olduqda bütün növbə qaytarılır. Filtr sayma və səhifələmədən əvvəl tətbiq edilir.</param>
    [HttpGet(ApiRoutes.Kitchen.GetOrders)]
    [ProducesResponseType(typeof(PaginatedList<KitchenOrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders([FromRoute] int restaurantId, [FromQuery] PaginationFilter pagination,
        CancellationToken cancellationToken, [FromQuery] int? statusId = null)
        => Ok(await Mediator.Send(new GetKitchenOrdersQuery(restaurantId, pagination, statusId), cancellationToken));
}
