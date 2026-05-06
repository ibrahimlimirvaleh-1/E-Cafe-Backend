using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class Role : AuditableSoftDeletableEntity<int>
{
    public string Name { get; set; } = null!;

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public ICollection<User> Users { get; set; } = new List<User>();
}
