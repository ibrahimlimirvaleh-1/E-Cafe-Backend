using ECafe.Domain.Enums;
using ECafe.Domain.Entities;
using System.Linq.Expressions;

namespace ECafe.Domain.Policies;

public static class UserProfileAccessPolicy
{
    private static readonly int[] RestaurantRoleIds =
        [(int)RoleCode.Owner, (int)RoleCode.Manager, (int)RoleCode.Waiter, (int)RoleCode.Kitchen];

    /// <summary>SQL-də aktiv müştərini və aktiv restoran profili qalmayan personal hesabını seçir; qlobal bloku saxlayır.</summary>
    public static Expression<Func<User, bool>> PersonalAccounts { get; } = user =>
        user.IsActive && (user.RoleId == (int)RoleCode.Customer ||
            RestaurantRoleIds.Contains(user.RoleId) && !user.UserRestaurants.Any(assignment => assignment.IsActive));

    /// <summary>Son restoran profili bağlandıqda hesabı bloklamadan yalnız şəxsi müştəri hüquqlarını saxlayır.</summary>
    public static int ResolveAccountRole(int storedRoleId, bool hasActiveRestaurantProfile)
        => !hasActiveRestaurantProfile && IsRestaurantRole(storedRoleId)
            ? (int)RoleCode.Customer
            : storedRoleId;

    /// <summary>Restoran profili tələb edən personal rollarını qlobal administrator və müştəri rolundan ayırır.</summary>
    public static bool IsRestaurantRole(int roleId)
        => RestaurantRoleIds.Contains(roleId);
}
