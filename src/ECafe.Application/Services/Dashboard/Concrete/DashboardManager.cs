using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Application.Services.Dashboard.Abstract;
using ECafe.Application.Services.Dashboard.Models;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Dashboard.Concrete;

public sealed class DashboardManager(IHttpContextAccessor accessor, IMapper mapper, IConfiguration configuration,
    IDashboardRepository repository, TimeProvider clock) : BaseManager(accessor, mapper, configuration), IDashboardService
{
    /// <summary>Canlı rolu yoxlayır, oxuma xidmətini idarə edir və ayrıca mapper ilə həmin rolun API cavabını yaradır.</summary>
    public async Task<DashboardResponse> GetAsync(RoleCode role, DashboardRequest request, CancellationToken cancellationToken)
    {
        var scope = CreateScope(role, request);
        var access = await repository.ResolveAccessAsync(scope, cancellationToken)
            ?? throw new ForbiddenException(ErrorCode.AccessDenied);
        var now = clock.GetUtcNow();
        var period = DashboardPeriodPolicy.Resolve(request.Period, request.From, request.To, access.TimeZone, now);
        var snapshot = await LoadSnapshotAsync(scope, period, now, request.PageIndex, cancellationToken);

        return DashboardResponseMapper.Map(role, request, access, period, now.UtcDateTime, snapshot);
    }

    /// <summary>Sorğunun rol və filtr sərhədlərini yoxlayır; superadmin görünüşünü restoran rolundan tələb etməyə icazə vermir.</summary>
    private DashboardScope CreateScope(RoleCode role, DashboardRequest request)
    {
        if (role is not (RoleCode.SuperAdmin or RoleCode.Owner or RoleCode.Manager))
            throw new ForbiddenException(ErrorCode.AccessDenied);
        if (role == RoleCode.SuperAdmin && !IsCurrentUserSuperAdmin())
            throw new ForbiddenException(ErrorCode.AccessDenied);
        if (request.RestaurantId is <= 0 || request.PageIndex is < 1 or > DashboardPeriodPolicy.MaximumPageIndex ||
            request.PaymentMode is not ("real" or "test"))
            throw new BadRequestException(ErrorCode.DashboardFilterInvalid);
        if (role == RoleCode.Manager && !request.RestaurantId.HasValue)
            throw new BadRequestException(ErrorCode.RestaurantContextRequired);

        return new(GetCurrentUserId(), role, request.RestaurantId, request.PaymentMode == "test");
    }

    /// <summary>Bir DbContext-in sorğularını ardıcıl icra edir; menecer üçün istifadə olunmayan maliyyə hesabatını yükləmir.</summary>
    private async Task<DashboardSnapshot> LoadSnapshotAsync(DashboardScope scope, DashboardPeriod period, DateTimeOffset now,
        int pageIndex, CancellationToken cancellationToken)
    {
        var isManager = scope.Role == RoleCode.Manager;
        var current = isManager ? new DashboardTotals(0, 0, 0, 0, 0, 0, 0)
            : await repository.GetTotalsAsync(scope, period.StartUtc, period.EndUtc, cancellationToken);
        var previous = isManager ? current
            : await repository.GetTotalsAsync(scope, period.PreviousStartUtc, period.PreviousEndUtc, cancellationToken);
        var today = RestaurantTimeZoneConverter.GetUtcDayRange(now, period.TimeZone);
        var live = await repository.GetLiveAsync(scope, now.UtcDateTime, today.StartUtc, today.EndUtc, cancellationToken);
        var chart = await repository.GetChartAsync(scope, period, cancellationToken);
        var sessions = isManager
            ? await repository.GetSessionsAsync(scope, pageIndex, cancellationToken) : null;
        var branches = isManager ? null
            : await repository.GetBranchesAsync(scope, period, now.UtcDateTime, pageIndex, cancellationToken);

        return new(current, previous, live, chart, branches, sessions);
    }
}
