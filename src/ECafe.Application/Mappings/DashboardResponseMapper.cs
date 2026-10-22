using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Application.Services.Dashboard.Models;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;

namespace ECafe.Application.Mappings;

public static class DashboardResponseMapper
{
    /// <summary>Oxunmuş icmalı API cavabına çevirir; menecer üçün ümumi maliyyə və filial nəticələrini daxil etmir.</summary>
    public static DashboardResponse Map(RoleCode role, DashboardRequest request, DashboardAccess access,
        DashboardPeriod period, DateTime nowUtc, DashboardSnapshot snapshot)
    {
        var current = snapshot.Current;
        var isManager = role == RoleCode.Manager;
        var sessions = isManager
            ? snapshot.Sessions!.Rows.Select(DashboardSessionMapper.Map).ToList()
            : new List<DashboardSessionRow>();

        return new(
            role == RoleCode.SuperAdmin ? "platform" : role == RoleCode.Owner ? "owner" : "manager",
            nowUtc, access.TimeZone, period.From, period.To, request.PaymentMode,
            access.Restaurants, access.HasMore,
            DashboardMetricMapper.Map(role, current, snapshot.Previous, snapshot.Live),
            isManager ? null : new(current.CollectedAmount, current.RefundedAmount,
                current.CollectedAmount - current.RefundedAmount, current.DepositCollectedAmount, current.SessionCollectedAmount),
            DashboardChartMapper.Map(role, period, snapshot.Chart),
            DashboardAlertMapper.Map(role, snapshot.Live, request.RestaurantId),
            isManager ? [] : snapshot.Branches!.Rows, sessions,
            request.PageIndex, DashboardPeriodPolicy.PageSize,
            isManager ? snapshot.Sessions!.Total : snapshot.Branches!.Total);
    }
}
