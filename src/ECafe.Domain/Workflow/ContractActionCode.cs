namespace ECafe.Domain.Workflow;

/// <summary>Yalnız müqavilə workflow-unun dəyişməyən əməliyyat identifikatorlarını saxlayır.</summary>
public static class ContractActionCode
{
    public const string SendForSignature = "sendForSignature";
    public const string Approve = "approve";
    public const string Activate = "activate";
    public const string Terminate = "terminate";
}
