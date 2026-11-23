using ECafe.Domain.Enums;

namespace ECafe.Domain.Policies;

public sealed record AnnouncementDestinationDefinition(string Key, IReadOnlyList<int> RoleIds, PermissionCode? Permission = null);

public static class ProductAnnouncementPolicy
{
    public const int BatchSize = 3;
    public const int AdminPageSize = 30;
    public const int MaximumTitleLength = 120;
    public const int MaximumBodyLength = 600;
    public const string CustomerProfile = nameof(CustomerProfile);
    public static IReadOnlyList<AnnouncementDestinationDefinition> Destinations { get; } = Array.AsReadOnly<AnnouncementDestinationDefinition>([
        new(CustomerProfile, [(int)RoleCode.Customer]), new("CustomerReservations", [(int)RoleCode.Customer]),
        new("CustomerOrders", [(int)RoleCode.Customer]),
        new("WaiterTables", [(int)RoleCode.Waiter], PermissionCode.ManageOrders),
        new("KitchenOrders", [(int)RoleCode.Kitchen], PermissionCode.ManageKitchenOrders),
        new("Inventory", [(int)RoleCode.SuperAdmin, (int)RoleCode.Owner, (int)RoleCode.Manager], PermissionCode.ViewInventory),
        new("DashboardReports", [(int)RoleCode.SuperAdmin, (int)RoleCode.Owner, (int)RoleCode.Manager], PermissionCode.ViewReports),
        new("AnnouncementAdmin", [(int)RoleCode.SuperAdmin])
    ]);

    /// <summary>Sərbəst URL qəbul etmir; yalnız hazır interfeysdə mövcud və rola uyğun təhlükəsiz keçidi həll edir.</summary>
    public static AnnouncementDestinationDefinition? FindDestination(string? key)
        => Destinations.FirstOrDefault(value => string.Equals(value.Key, key, StringComparison.Ordinal));

    /// <summary>Cari DB icazələri və şəxsi hesab uyğunluğu üzrə göstərilə bilən keçid açarlarını seçir.</summary>
    public static string[] AllowedDestinations(int roleId, IReadOnlyCollection<int> permissions, bool hasActiveStaffProfile)
        => Destinations.Where(value => value.RoleIds.Contains(roleId) &&
            (!value.Permission.HasValue || permissions.Contains((int)value.Permission.Value)) &&
            (value.Key != CustomerProfile || !hasActiveStaffProfile)).Select(value => value.Key).ToArray();
}
