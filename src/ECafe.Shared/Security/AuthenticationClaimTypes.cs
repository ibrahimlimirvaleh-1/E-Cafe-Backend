namespace ECafe.Shared.Security;

// JWT yaradılması və oxunması üçün mövcud xarici claim adları dəyişmədən saxlanılır.
public static class AuthenticationClaimTypes
{
    public const string UserId = "userId";
    public const string Name = "name";
    public const string Surname = "surname";
    public const string Email = "email";
    public const string IsActive = "isActive";
    public const string SessionVersion = "sessionVersion";
    public const string SessionId = "sessionId";
    public const string FileUrl = "fileUrl";
    public const string RestaurantId = "restaurantId";
    public const string RestaurantIds = "restaurantIds";
    public const string RestaurantRoles = "restaurantRoles";
    public const string RoleName = "roleName";
    public const string Permission = "permission";
}
