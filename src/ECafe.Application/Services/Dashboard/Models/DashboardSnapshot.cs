using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Entities;

namespace ECafe.Application.Services.Dashboard.Models;

public sealed record DashboardSnapshot(
    DashboardTotals Current,
    DashboardTotals Previous,
    DashboardLive Live,
    IReadOnlyList<DashboardHour> Chart,
    DashboardPage<DashboardBranchRow>? Branches,
    DashboardPage<TableSession>? Sessions);
