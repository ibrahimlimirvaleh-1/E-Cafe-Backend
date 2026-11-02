using ECafe.Application.Common.Exceptions;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;

namespace ECafe.Domain.Policies;

public static class DashboardReportPolicy
{
    public const int PageSize = 20;
    public const int ExportLimit = 5000;

    /// <summary>Hesabat növünü və uyğun status filtrini yoxlayır; menecerə ümumi ödəniş hesabatı açmır.</summary>
    public static DashboardReportKind Resolve(RoleCode role, string kind, int? statusId, string paymentEvent)
    {
        var parsed = kind switch
        {
            "orders" => DashboardReportKind.Orders,
            "reservations" => DashboardReportKind.Reservations,
            "payments" => DashboardReportKind.Payments,
            _ => throw new BadRequestException(ErrorCode.DashboardFilterInvalid)
        };
        if (parsed == DashboardReportKind.Payments && role == RoleCode.Manager)
            throw new ForbiddenException(ErrorCode.AccessDenied);
        if (paymentEvent is not ("all" or "receipt" or "refund") || parsed != DashboardReportKind.Payments && paymentEvent != "all")
            throw new BadRequestException(ErrorCode.DashboardFilterInvalid);
        if (statusId.HasValue)
        {
            var valid = parsed switch
            {
                DashboardReportKind.Orders => Enum.GetValues<OrderStatus>().Any(status => StatusIds.Build(StatusType.Order, (int)status) == statusId),
                DashboardReportKind.Reservations => Enum.GetValues<ReservationStatus>().Any(status => StatusIds.Reservation(status) == statusId),
                _ => false
            };
            if (!valid) throw new BadRequestException(ErrorCode.DashboardFilterInvalid);
        }
        return parsed;
    }
}
