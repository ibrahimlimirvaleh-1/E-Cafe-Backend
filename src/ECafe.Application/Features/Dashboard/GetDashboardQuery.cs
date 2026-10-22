using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Services.Dashboard.Abstract;
using ECafe.Domain.Enums;
using MediatR;

namespace ECafe.Application.Features.Dashboard;

public sealed record GetDashboardQuery(RoleCode Role, DashboardRequest Filter) : IRequest<DashboardResponse>;

public sealed class GetDashboardQueryHandler(IDashboardService service) : IRequestHandler<GetDashboardQuery, DashboardResponse>
{
    /// <summary>İcazə, tarix və rol üzrə icmalı tətbiq xidmətinə ötürür; controller-də hesablama saxlamır.</summary>
    public Task<DashboardResponse> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
        => service.GetAsync(request.Role, request.Filter, cancellationToken);
}
