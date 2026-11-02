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

    /// <summary>Platforma administratoruna tarix və status üzrə səhifələnmiş sifariş, rezervasiya və pul hərəkəti hesabatını qaytarır.</summary>
    /// <remarks>Kind orders/reservations/payments; PageSize sabit 20-dir. StatusId yalnız ilk iki hesabat üçündür. PaymentEvent all/receipt/refund yalnız pul hərəkətindədir. Maksimum 92 gün; tarixlər restoran saat qurşağındadır, cavab vaxtları UTC-dir.</remarks>
    [HttpGet(ApiRoutes.Dashboard.PlatformReports)]
    [ProducesResponseType(typeof(DashboardReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PlatformReports([FromQuery] DashboardReportRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetDashboardReportQuery(RoleCode.SuperAdmin, request), cancellationToken));

    /// <summary>Sahibkarın canlı aktiv təyinatla giriş hüququ olan restoranlarının hesabatını qaytarır; digər restoran ID-si hüququ genişləndirmir.</summary>
    [HttpGet(ApiRoutes.Dashboard.OwnerReports)]
    [ProducesResponseType(typeof(DashboardReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> OwnerReports([FromQuery] DashboardReportRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetDashboardReportQuery(RoleCode.Owner, request), cancellationToken));

    /// <summary>Menecerə öz restoranının sifariş və rezervasiya hesabatını qaytarır; pul hərəkəti hesabatı və toplu maliyyə göstəricisi verilmir.</summary>
    [HttpGet(ApiRoutes.Dashboard.ManagerReports)]
    [ProducesResponseType(typeof(DashboardReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ManagerReports([FromQuery] DashboardReportRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetDashboardReportQuery(RoleCode.Manager, request), cancellationToken));

    /// <summary>Platforma administratorunun filtrli hesabatını UTF-8 CSV kimi çıxarır; 5000-dən çox sətirdə filtrin daraldılmasını tələb edir.</summary>
    [HttpGet(ApiRoutes.Dashboard.PlatformReportExport)]
    [Produces("text/csv")]
    public async Task<IActionResult> PlatformReportExport([FromQuery] DashboardReportRequest request, CancellationToken cancellationToken)
    {
        var report = await Mediator.Send(new ExportDashboardReportQuery(RoleCode.SuperAdmin, request), cancellationToken);
        return File(report.Content, "text/csv; charset=utf-8", report.FileName);
    }

    /// <summary>Sahibkarın hesabatını eyni canlı restoran hüquqları və limitlərlə CSV-yə çıxarır; daxili qeyd və ödəniş rekvizitləri daxil deyil.</summary>
    [HttpGet(ApiRoutes.Dashboard.OwnerReportExport)]
    [Produces("text/csv")]
    public async Task<IActionResult> OwnerReportExport([FromQuery] DashboardReportRequest request, CancellationToken cancellationToken)
    {
        var report = await Mediator.Send(new ExportDashboardReportQuery(RoleCode.Owner, request), cancellationToken);
        return File(report.Content, "text/csv; charset=utf-8", report.FileName);
    }

    /// <summary>Menecerin yalnız icazəli sifariş və rezervasiya hesabatını eyni filtr üzrə CSV-yə çıxarır; pul hərəkəti ixracını rədd edir.</summary>
    [HttpGet(ApiRoutes.Dashboard.ManagerReportExport)]
    [Produces("text/csv")]
    public async Task<IActionResult> ManagerReportExport([FromQuery] DashboardReportRequest request, CancellationToken cancellationToken)
    {
        var report = await Mediator.Send(new ExportDashboardReportQuery(RoleCode.Manager, request), cancellationToken);
        return File(report.Content, "text/csv; charset=utf-8", report.FileName);
    }
}
