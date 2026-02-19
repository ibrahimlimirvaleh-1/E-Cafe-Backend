using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class StatusType : AuditableSoftDeletableEntity<int>
{
    public string Name { get; set; } = null!;

    public virtual ICollection<Status> Statuses { get; set; } = new List<Status>();
}
