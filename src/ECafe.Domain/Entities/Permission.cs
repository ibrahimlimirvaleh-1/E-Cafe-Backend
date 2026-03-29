using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;
public partial class Permission : AuditableSoftDeletableEntity<int>
{
    public string Name { get; set; } = null!;

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
