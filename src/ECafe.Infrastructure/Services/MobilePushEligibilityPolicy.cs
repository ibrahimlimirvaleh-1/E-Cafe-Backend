using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Services;

internal static class MobilePushEligibilityPolicy
{
    private static readonly int[] StaffRoleIds =
        [(int)RoleCode.Owner, (int)RoleCode.Manager, (int)RoleCode.Waiter, (int)RoleCode.Kitchen];

    /// <summary>
    /// Push qeydiyyatı və göndərişi üçün eyni icazəni tətbiq edir: aktiv işçi aidiyyəti,
    /// adminin açdığı restoran mobil modulu və qüvvədə olan müqavilə tələb olunur.
    /// Restoran göstərildikdə başqa restorandakı aidiyyət icazə hesab edilmir.
    /// </summary>
    public static IQueryable<UserRestaurant> EligibleStaff(ECafeDbContext context, int? restaurantId = null)
    {
        var activeContractStatusId = StatusIds.Contract(ContractStatus.Active);
        return context.UserRestaurants.Where(assignment =>
            (!restaurantId.HasValue || assignment.RestaurantId == restaurantId.Value) &&
            assignment.IsActive && assignment.User.IsActive &&
            StaffRoleIds.Contains(assignment.RoleId) &&
            assignment.Restaurant.IsActive && assignment.Restaurant.MobilePushEnabled &&
            assignment.Restaurant.ShowMobileDownloadLink &&
            assignment.Restaurant.Contracts.Any(contract => contract.StatusId == activeContractStatusId));
    }
}
