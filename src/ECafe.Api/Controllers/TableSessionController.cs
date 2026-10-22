using ECafe.Application.Features.Queries.TableSession;
using ECafe.Application.DTOs.TableSession;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class TableSessionController : BaseController
{
    /// <summary>Ofisiantın öz açıq masalarındakı hazır sifarişləri ən çox gözləyəndən başlayaraq gətirir.</summary>
    /// <remarks>Ofisiant token sahibindən və bazadakı aktiv təyinatdan müəyyən edilir; təqdim etmə əməliyyatı ayrıca mövcud endpointdə yenidən yoxlanılır.</remarks>
    [HttpGet(ApiRoutes.TableSession.GetMyReadyOrders)]
    [HasPermission(PermissionCode.ManageOrders)]
    [ProducesResponseType(typeof(PaginatedList<WaiterReadyOrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReadyOrders([FromRoute] int restaurantId,
        [FromQuery] PaginationFilter pagination, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetMyReadyOrdersQuery(restaurantId, pagination), cancellationToken));

    /// <summary>Ofisiantın seçilmiş restoranda özünə bağlı açıq masa sessiyalarını səhifələnmiş qaytarır.</summary>
    /// <remarks>Ofisiant ID-si token sahibindən götürülür. Bağlı, ləğv edilmiş və başqa ofisiantın sessiyaları göstərilmir.</remarks>
    [HttpGet(ApiRoutes.TableSession.GetMyOpenSessions)]
    [HasPermission(PermissionCode.ManageOrders)]
    [ProducesResponseType(typeof(PaginatedList<WaiterTableSessionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOpenSessions([FromRoute] int restaurantId,
        [FromQuery] PaginationFilter pagination, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetMyOpenSessionsQuery(restaurantId, pagination), cancellationToken));

    /// <summary>Ofisiantın özünə bağlı bir açıq masa sessiyasının xidmət məlumatlarını gətirir.</summary>
    /// <remarks>Başqa ofisianta/restorana aid, bağlı və ya mövcud olmayan sessiya üçün eyni 404 cavabı qaytarılır. Maliyyə və şəxsi əlaqə məlumatları göstərilmir.</remarks>
    [HttpGet(ApiRoutes.TableSession.GetMyOpenSession)]
    [HasPermission(PermissionCode.ManageOrders)]
    [ProducesResponseType(typeof(WaiterTableSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOpenSession([FromRoute] int restaurantId,
        [FromRoute] int tableSessionId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetMyOpenSessionQuery(restaurantId, tableSessionId), cancellationToken));

    /// <summary>Menecer və sahibkar üçün işgünü bitmiş, amma bağlanmamış masa sessiyalarının yoxlama siyahısını gətirir.</summary>
    /// <remarks>Tarix filtrindən asılı deyil. Restoranın yerli iş saatlarını, gecəyarısından sonrakı bağlanmanı və menecerin növbəti yoxlama qərarını nəzərə alır. Sessiyanı və rezervasiyanı avtomatik tamamlamır. Yalnız həmin restoranın aktiv meneceri/sahibkarı görə bilər.</remarks>
    [HasPermission(PermissionCode.ManageReservations)]
    [HttpGet(ApiRoutes.TableSession.CloseReviews)]
    [ProducesResponseType(typeof(PaginatedList<WaiterTableSessionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCloseReviews([FromRoute] int restaurantId, [FromQuery] PaginationFilter pagination, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetSessionCloseReviewsQuery(restaurantId, pagination), cancellationToken));
}
