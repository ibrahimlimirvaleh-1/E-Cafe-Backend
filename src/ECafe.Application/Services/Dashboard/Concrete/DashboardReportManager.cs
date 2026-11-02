using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Application.Services.Dashboard.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Dashboard.Concrete;

public sealed class DashboardReportManager(IHttpContextAccessor accessor, IMapper mapper, IConfiguration configuration,
    IDashboardRepository accessRepository, IDashboardReportRepository reports, TimeProvider clock)
    : BaseManager(accessor, mapper, configuration), IDashboardReportService
{
    /// <summary>Cari rolun canlı icazəsi ilə filtrli hesabat səhifəsini gətirir; bütün yekunları tam filtr üzrə saxlayır.</summary>
    public Task<DashboardReportResponse> GetAsync(RoleCode role, DashboardReportRequest request, CancellationToken cancellationToken)
        => ReadAsync(role, request, false, cancellationToken);

    /// <summary>Eyni səlahiyyət və filtrlərlə məhdud tam ixrac yaradır; cədvəlin yalnız cari səhifəsini tam hesabat kimi təqdim etmir.</summary>
    public async Task<DashboardReportFile> ExportAsync(RoleCode role, DashboardReportRequest request, CancellationToken cancellationToken)
        => DashboardReportCsvMapper.Map(await ReadAsync(role, request, true, cancellationToken));

    /// <summary>İcmalla ortaq rol/tarix siyasətini və bazadakı canlı restoran təyinatını yoxlayıb yalnız seçilmiş hesabatı yükləyir.</summary>
    private async Task<DashboardReportResponse> ReadAsync(RoleCode role, DashboardReportRequest request, bool export,
        CancellationToken cancellationToken)
    {
        DashboardScopePolicy.Validate(role, IsCurrentUserSuperAdmin(), request.RestaurantId, request.PageIndex, request.PaymentMode);
        var kind = DashboardReportPolicy.Resolve(role, request.Kind, request.StatusId, request.PaymentEvent);
        var scope = new DashboardScope(GetCurrentUserId(), role, request.RestaurantId, request.PaymentMode == "test");
        var access = await accessRepository.ResolveAccessAsync(scope, cancellationToken)
            ?? throw new ForbiddenException(ErrorCode.AccessDenied);
        var now = clock.GetUtcNow();
        var period = DashboardPeriodPolicy.Resolve(request.Period, request.From, request.To, access.TimeZone, now);
        var data = await reports.GetReportAsync(scope, period, kind, request.StatusId, request.PaymentEvent,
            request.PageIndex, export, cancellationToken);
        return DashboardReportResponseMapper.Map(role, request, period, now.UtcDateTime, kind, data, export);
    }
}
