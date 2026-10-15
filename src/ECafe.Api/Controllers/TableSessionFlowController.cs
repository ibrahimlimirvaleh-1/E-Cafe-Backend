using ECafe.Application.DTOs.TableSession;
using ECafe.Application.Features.Commands.TableSession;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class TableSessionFlowController : BaseController
{
    /// <summary>Menecer və ya sahibkar faktiki xidmətin davam etdiyini səbəblə təsdiqləyir və növbəti yoxlama vaxtını seçir.</summary>
    /// <remarks>Yalnız bağlanması yoxlanmalı olan açıq sessiya üçündür. ReviewAgainAt gələcəkdə, TableSessions:CloseReview:MaxContinuationHours (standart 24 saat) daxilində, Reason isə məcburidir (standart 500 simvol). Razılaşdırılmış çıxış vaxtı, rezervasiya statusu, sifariş və ödəniş dəyişmir. Aktiv rol DB-dən təkrar yoxlanır. Qərar audit və rezervasiya tarixçəsinə yazılır. Növbəti yoxlama vaxtında yalnız bir yeni bildiriş dövrü başlanır.</remarks>
    [HasPermission(PermissionCode.ManageReservations)]
    [HttpPost(ApiRoutes.TableSessionFlow.ContinueService)]
    [ProducesResponseType(typeof(WaiterTableSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ContinueService([FromRoute] int restaurantId, [FromRoute] int tableSessionId,
        [FromBody] ContinueTableServiceRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new ContinueTableServiceCommand(restaurantId, tableSessionId, request), cancellationToken));
}
