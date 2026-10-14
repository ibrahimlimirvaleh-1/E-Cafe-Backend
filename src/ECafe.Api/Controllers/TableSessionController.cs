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
[HasPermission(PermissionCode.ManageOrders)]
public sealed class TableSessionController : BaseController
{
    /// <summary>Ofisiantın seçilmiş restoranda özünə bağlı açıq masa sessiyalarını səhifələnmiş qaytarır.</summary>
    /// <remarks>Ofisiant ID-si token sahibindən götürülür. Bağlı, ləğv edilmiş və başqa ofisiantın sessiyaları göstərilmir.</remarks>
    [HttpGet(ApiRoutes.TableSession.GetMyOpenSessions)]
    [ProducesResponseType(typeof(PaginatedList<WaiterTableSessionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOpenSessions([FromRoute] int restaurantId,
        [FromQuery] PaginationFilter pagination, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetMyOpenSessionsQuery(restaurantId, pagination), cancellationToken));

    /// <summary>Ofisiantın özünə bağlı bir açıq masa sessiyasının xidmət məlumatlarını gətirir.</summary>
    /// <remarks>Başqa ofisianta/restorana aid, bağlı və ya mövcud olmayan sessiya üçün eyni 404 cavabı qaytarılır. Maliyyə və şəxsi əlaqə məlumatları göstərilmir.</remarks>
    [HttpGet(ApiRoutes.TableSession.GetMyOpenSession)]
    [ProducesResponseType(typeof(WaiterTableSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOpenSession([FromRoute] int restaurantId,
        [FromRoute] int tableSessionId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetMyOpenSessionQuery(restaurantId, tableSessionId), cancellationToken));
}
