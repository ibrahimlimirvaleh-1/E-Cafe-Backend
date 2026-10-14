using ECafe.Application.DTOs.Order;
using ECafe.Application.Features.Commands.Order;
using ECafe.Application.Features.Queries.Order;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
[HasPermission(PermissionCode.ManageOrders)]
public sealed class OrderController : BaseController
{
    /// <summary>Ofisiant öz açıq masa sessiyasına sifariş yaradır; mətbəxə göndərmir.</summary>
    /// <remarks>Idempotency-Key yeni sifariş üçün yeni UUID olmalıdır. Şəbəkə xətasından sonra eyni sessiya və eyni məzmun ilə həmin açarı təkrar göndərin. İlk cavab 201, təkrar 200, açarın fərqli məzmunla istifadəsi 409 olur. Sessiya hər çağırışda açıq və cari ofisianta aid olmalıdır. Qiymət serverdə hesablanır; ExpectedUnitPrice yalnız köhnə menyu qiymətini aşkar etmək üçündür. Stokdan silinmə, ödəniş və mətbəx bildirişi edilmir.</remarks>
    [RequireActiveRestaurantContract]
    [HttpPost(ApiRoutes.Order.Create)]
    [ProducesResponseType(typeof(SessionOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(SessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromRoute] int restaurantId, [FromRoute] int tableSessionId,
        [FromHeader(Name = "Idempotency-Key")] Guid requestId, [FromBody] CreateWaiterOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateWaiterOrderCommand(restaurantId, tableSessionId, requestId, request), cancellationToken);
        return result.IsReplay ? Ok(result.Order) : CreatedAtAction(nameof(GetById),
            new { restaurantId, tableSessionId, orderId = result.Order.Id }, result.Order);
    }

    /// <summary>Ofisiantın öz açıq masa sessiyasının sifarişlərini yenidən köhnəyə səhifələnmiş qaytarır.</summary>
    [HttpGet(ApiRoutes.Order.GetForSession)]
    [ProducesResponseType(typeof(PaginatedList<SessionOrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForSession([FromRoute] int restaurantId, [FromRoute] int tableSessionId,
        [FromQuery] PaginationFilter pagination, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetSessionOrdersQuery(restaurantId, tableSessionId, pagination), cancellationToken));

    /// <summary>Ofisiantın öz açıq masa sessiyasındakı sifarişin saxlanılmış məhsul və qiymət nüsxələrini qaytarır.</summary>
    [HttpGet(ApiRoutes.Order.GetById)]
    [ProducesResponseType(typeof(SessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById([FromRoute] int restaurantId, [FromRoute] int tableSessionId,
        [FromRoute] int orderId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetSessionOrderQuery(restaurantId, tableSessionId, orderId), cancellationToken));
}
