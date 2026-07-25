using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class WorkflowActionRule : BaseEntity<int>
{
    public string FlowCode { get; set; } = null!;

    public int StatusId { get; set; }

    public int RoleId { get; set; }

    public string ActionCode { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string HttpMethod { get; set; } = null!;

    public string EndpointTemplate { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool RequiresConfirmation { get; set; }

    public bool IsEnabled { get; set; } = true;

    public virtual Status Status { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;
}
