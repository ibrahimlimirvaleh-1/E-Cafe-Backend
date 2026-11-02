using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Services.Dashboard.Abstract;
using ECafe.Domain.Enums;
using MediatR;

namespace ECafe.Application.Features.Dashboard;

public sealed record GetDashboardReportQuery(RoleCode Role, DashboardReportRequest Filter) : IRequest<DashboardReportResponse>;

public sealed class GetDashboardReportQueryHandler(IDashboardReportService service)
    : IRequestHandler<GetDashboardReportQuery, DashboardReportResponse>
{
    /// <summary>Hesabat sorğusunu rol və restoran yoxlaması aparan tətbiq xidmətinə ötürür.</summary>
    public Task<DashboardReportResponse> Handle(GetDashboardReportQuery request, CancellationToken cancellationToken)
        => service.GetAsync(request.Role, request.Filter, cancellationToken);
}
