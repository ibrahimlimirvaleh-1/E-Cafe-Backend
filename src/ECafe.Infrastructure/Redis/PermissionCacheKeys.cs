using System.Globalization;

namespace ECafe.Infrastructure.Redis;

public static class PermissionCacheKeys
{
    /// <summary>İcazənin oxunması və ləğvi üçün eyni versiyalı Redis açarını yaradır; mövcud v3 keş adını dəyişmir.</summary>
    public static string ForRole(int roleId) => "role_permissions_v3_" + roleId.ToString(CultureInfo.InvariantCulture);
}
