using System.Data;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Filtrli hesabatın yekunu və sətirlərini eyni sabit oxuma snapshot-ında yükləyir; ixracı məhdudlaşdırır və heç bir biznes qeydi dəyişmir.</summary>
    public async Task<DashboardReportData> GetReportAsync(DashboardScope scope, DashboardPeriod period, DashboardReportKind kind,
        int? statusId, string paymentEvent, int pageIndex, bool export, CancellationToken cancellationToken)
    {
        if (scope.Role == RoleCode.Manager && !DashboardReportCatalog.Get(kind).AllowsManager)
            throw new ForbiddenException(ErrorCode.AccessDenied);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        var data = kind switch
        {
            DashboardReportKind.Orders => await ReadOrderReportAsync(scope, period, statusId, pageIndex, export, cancellationToken),
            DashboardReportKind.Reservations => await ReadReservationReportAsync(scope, period, statusId, pageIndex, export, cancellationToken),
            DashboardReportKind.Payments => await ReadPaymentReportAsync(scope, period, paymentEvent, pageIndex, export, cancellationToken),
            DashboardReportKind.Products => await ReadProductReportAsync(scope, period, pageIndex, export, cancellationToken),
            DashboardReportKind.Kitchen => await ReadKitchenReportAsync(scope, period, pageIndex, export, cancellationToken),
            DashboardReportKind.Cancellations => await ReadCancellationReportAsync(scope, period, pageIndex, export, cancellationToken),
            DashboardReportKind.ReservationOutcomes => await ReadReservationOutcomeReportAsync(scope, period, pageIndex, export, cancellationToken),
            DashboardReportKind.Tables => await ReadTableUsageReportAsync(scope, period, pageIndex, export, cancellationToken),
            DashboardReportKind.Settlements => await ReadSettlementReportAsync(scope, period, pageIndex, export, cancellationToken),
            DashboardReportKind.Branches => await ReadBranchComparisonReportAsync(scope, period, pageIndex, export, cancellationToken),
            DashboardReportKind.Stock => await ReadStockReportAsync(scope, period, pageIndex, export, cancellationToken),
            _ => throw new ForbiddenException(ErrorCode.AccessDenied)
        };
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return data;
    }

    /// <summary>İxracın səssiz kəsilməsinə yol vermir; adi cədvəl üçün təhlükəsiz və sabit səhifə ölçüsünü qaytarır.</summary>
    private static (int Skip, int Take) ReportPage(int count, int pageIndex, bool export)
    {
        if (export && count > DashboardReportPolicy.ExportLimit)
            throw new BadRequestException(ErrorCode.DashboardExportLimitExceeded);
        return export ? (0, DashboardReportPolicy.ExportLimit) : ((pageIndex - 1) * DashboardReportPolicy.PageSize, DashboardReportPolicy.PageSize);
    }
}
