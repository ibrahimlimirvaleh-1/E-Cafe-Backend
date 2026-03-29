using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class RolePermission : AuditableSoftDeletableEntity<int>
    {
        public int RoleId { get; set; }
        public int PermissionId { get; set; }

        public virtual Role Role { get; set; } = null!;
        public virtual Permission Permission { get; set; } = null!;
    }
}
