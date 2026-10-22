using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Features.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[HasPermission(PermissionCode.ViewReports)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DashboardController : BaseController
{
    /// <summary>Platforma administratorunun real aqreqatlarını gətirir; cari istifadəçinin canlı superadmin rolu ayrıca yoxlanılır.</summary>
    /// <remarks>Period: today, week, month, custom. Custom üçün from/to tarixləri və maksimum 92 gün tələb olunur. PaymentMode real və ya test-dir; test heç vaxt real daxilolmaya qarışmır. Seçilmiş restoran yoxdursa hesabat saat qurşağı Asia/Baku-dur.</remarks>
    [HttpGet(ApiRoutes.Dashboard.Platform)]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Platform([FromQuery] DashboardRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetDashboardQuery(RoleCode.SuperAdmin, request), cancellationToken));

    /// <summary>Sahibkarın yalnız aktiv təyinatlı öz restoranlarının icmalını qaytarır; seçilən filial eyni sərhəddə qalır.</summary>
    [HttpGet(ApiRoutes.Dashboard.Owner)]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Owner([FromQuery] DashboardRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetDashboardQuery(RoleCode.Owner, request), cancellationToken));

    /// <summary>Menecerin təyin olunduğu restoranın canlı xidmət icmalını və səhifələnmiş açıq masalarını qaytarır.</summary>
    /// <remarks>RestaurantId məcburidir. Platforma istifadəçi sayları və ümumi pul daxilolmaları bu görünüşə daxil edilmir.</remarks>
    [HttpGet(ApiRoutes.Dashboard.Manager)]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Manager([FromQuery] DashboardRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetDashboardQuery(RoleCode.Manager, request), cancellationToken));
}
