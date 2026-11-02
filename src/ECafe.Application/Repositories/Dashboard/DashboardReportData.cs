using ECafe.Application.DTOs.Dashboard;

namespace ECafe.Application.Repositories.Dashboard;

public sealed record DashboardReportData(DashboardReportTotals Totals, IReadOnlyList<DashboardOrderReportRow> Orders,
    IReadOnlyList<DashboardReservationReportRow> Reservations, IReadOnlyList<DashboardPaymentReportRow> Payments);
