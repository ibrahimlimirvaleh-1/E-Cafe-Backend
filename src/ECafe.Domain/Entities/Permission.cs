using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;
public partial class Permission : AuditableSoftDeletableEntity<int>
{
    public string Name { get; set; } = null!;

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}
