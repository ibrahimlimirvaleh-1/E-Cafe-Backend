using ECafe.Domain.Enums;

namespace ECafe.Domain.Workflow;

public static class WorkflowFlowCode
{
    public static string FromStatusType(StatusType statusType)
        => statusType.ToString().ToLowerInvariant();
}
