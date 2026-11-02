using ECafe.Application.Common.Exceptions;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Constants;

namespace ECafe.Domain.Policies;

public static class DashboardScopePolicy
{
    /// <summary>İcmal və hesabatın eyni rol, restoran, səhifə və ödəniş rejimi sərhədlərindən keçməsini təmin edir; canlı təyinat repository-də yoxlanır.</summary>
    public static void Validate(RoleCode role, bool isSuperAdmin, int? restaurantId, int pageIndex, string paymentMode)
    {
        if (role is not (RoleCode.SuperAdmin or RoleCode.Owner or RoleCode.Manager) || role == RoleCode.SuperAdmin && !isSuperAdmin)
            throw new ForbiddenException(ErrorCode.AccessDenied);
        if (restaurantId is <= 0 || pageIndex is < 1 or > DashboardPeriodPolicy.MaximumPageIndex ||
            paymentMode is not (DashboardFilterCodes.PaymentMode.Real or DashboardFilterCodes.PaymentMode.Test))
            throw new BadRequestException(ErrorCode.DashboardFilterInvalid);
        if (role == RoleCode.Manager && !restaurantId.HasValue)
            throw new BadRequestException(ErrorCode.RestaurantContextRequired);
    }
}
