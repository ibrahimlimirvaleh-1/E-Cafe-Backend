using ECafe.Domain.Enums;
using ECafe.Domain.Entities;
using System.Linq.Expressions;
using ECafe.Domain.Exceptions;

namespace ECafe.Domain.Policies;

public static class UserProfileAccessPolicy
{
    public static IReadOnlyCollection<int> RestaurantRoleIds { get; } = Array.AsReadOnly<int>(
        [(int)RoleCode.Owner, (int)RoleCode.Manager, (int)RoleCode.Waiter, (int)RoleCode.Kitchen]);

    /// <summary>Yalnız həqiqi aktiv müştəri hesablarını seçir; profilsiz personalı müştəriyə çevirmir.</summary>
    public static Expression<Func<User, bool>> PersonalAccounts { get; } = user =>
        user.IsActive && user.RoleId == (int)RoleCode.Customer;

    /// <summary>Personal üçün aktiv işçi aidiyyəti tələb edir; adi müştəri və qlobal administratoru bu tələbdən ayırır.</summary>
    public static bool HasRequiredStaffProfile(User user)
        => !IsRestaurantRole(user.RoleId) || user.UserRestaurants.Any(assignment =>
            assignment.IsActive && !assignment.IsDeleted && IsRestaurantRole(assignment.RoleId));

    /// <summary>Profilsiz personalın yeni token və profil cavabı almasını eyni xəta ilə dayandırır.</summary>
    public static void EnsureHasRequiredStaffProfile(User user)
    {
        if (!HasRequiredStaffProfile(user))
            throw new UnauthorizedException(ErrorCode.StaffProfileInactive);
    }

    /// <summary>Restoran profili tələb edən personal rollarını qlobal administrator və müştəri rolundan ayırır.</summary>
    public static bool IsRestaurantRole(int roleId)
        => RestaurantRoleIds.Contains(roleId);
}
