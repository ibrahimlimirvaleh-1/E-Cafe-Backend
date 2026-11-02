using ECafe.Domain.Enums;

namespace ECafe.Domain.Policies;

public sealed record DashboardReportDefinition(DashboardReportKind Kind, string Code, bool UsesPaymentMode, bool AllowsManager);
