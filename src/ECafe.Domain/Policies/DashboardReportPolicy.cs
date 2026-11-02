using ECafe.Application.Common.Exceptions;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Constants;

namespace ECafe.Domain.Policies;

public static class DashboardReportPolicy
{
    public const int PageSize = 20;
    public const int ExportLimit = 5000;

    /// <summary>Hesabat növünü və uyğun status filtrini yoxlayır; menecerə ümumi ödəniş hesabatı açmır.</summary>
    public static DashboardReportKind Resolve(RoleCode role, string kind, int? statusId, string paymentEvent)
    {
        if (!DashboardReportCatalog.TryGet(kind, out var definition)) throw new BadRequestException(ErrorCode.DashboardFilterInvalid);
        var parsed = definition.Kind;
        if (!definition.AllowsManager && role == RoleCode.Manager)
            throw new ForbiddenException(ErrorCode.AccessDenied);
        if (paymentEvent is not (DashboardFilterCodes.PaymentEvent.All or DashboardFilterCodes.PaymentEvent.Receipt or DashboardFilterCodes.PaymentEvent.Refund) ||
            parsed != DashboardReportKind.Payments && paymentEvent != DashboardFilterCodes.PaymentEvent.All)
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
