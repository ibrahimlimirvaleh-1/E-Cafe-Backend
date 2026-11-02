using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;

namespace ECafe.Application.Mappings;

public static class DashboardReportResponseMapper
{
    /// <summary>Repository snapshot-ını yalnız seçilmiş hesabatın məlumatları ilə API cavabına çevirir; bütün yekunlar səhifədən deyil tam filtrdən gəlir.</summary>
    public static DashboardReportResponse Map(RoleCode role, DashboardReportRequest request, DashboardPeriod period,
        DateTime nowUtc, DashboardReportKind kind, DashboardReportData data, bool export)
        => new(request.Kind, nowUtc, period.TimeZone, period.From, period.To, request.PaymentMode,
            DashboardReportMetricMapper.Map(kind, role, data.Totals), data.Orders, data.Reservations, data.Payments,
            export ? 1 : request.PageIndex, export ? DashboardReportPolicy.ExportLimit : DashboardReportPolicy.PageSize, data.Totals.RowCount);
}
