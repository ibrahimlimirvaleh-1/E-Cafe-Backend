namespace ECafe.Application.DTOs.Workflow;

public class WorkflowActionResponse
{
    public string Code { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string HttpMethod { get; set; } = null!;

    public string Endpoint { get; set; } = null!;

    public bool RequiresConfirmation { get; set; }

    public int SortOrder { get; set; }
}
