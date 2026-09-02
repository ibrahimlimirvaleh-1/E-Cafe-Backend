using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class Role : AuditableSoftDeletableEntity<int>
{
    public string Name { get; set; } = null!;

    public bool IsStaffAssignable { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public virtual ICollection<UserRestaurant> UserRestaurants { get; set; } = new List<UserRestaurant>();

    public ICollection<User> Users { get; set; } = new List<User>();
}
