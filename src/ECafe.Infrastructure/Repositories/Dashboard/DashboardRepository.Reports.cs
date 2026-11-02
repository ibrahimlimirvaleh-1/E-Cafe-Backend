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
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        var data = kind switch
        {
            DashboardReportKind.Orders => await ReadOrderReportAsync(scope, period, statusId, pageIndex, export, cancellationToken),
            DashboardReportKind.Reservations => await ReadReservationReportAsync(scope, period, statusId, pageIndex, export, cancellationToken),
            DashboardReportKind.Payments when scope.Role != RoleCode.Manager => await ReadPaymentReportAsync(scope, period, paymentEvent, pageIndex, export, cancellationToken),
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
