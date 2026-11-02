using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Services.Dashboard.Abstract;
using ECafe.Domain.Enums;
using MediatR;

namespace ECafe.Application.Features.Dashboard;

public sealed record ExportDashboardReportQuery(RoleCode Role, DashboardReportRequest Filter) : IRequest<DashboardReportFile>;

public sealed class ExportDashboardReportQueryHandler(IDashboardReportService service)
    : IRequestHandler<ExportDashboardReportQuery, DashboardReportFile>
{
    /// <summary>CSV ixracını normal hesabatla eyni səlahiyyət və filtr yoxlamasına yönləndirir.</summary>
    public Task<DashboardReportFile> Handle(ExportDashboardReportQuery request, CancellationToken cancellationToken)
        => service.ExportAsync(request.Role, request.Filter, cancellationToken);
}
