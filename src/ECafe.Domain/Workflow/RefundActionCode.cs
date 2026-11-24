namespace ECafe.Domain.Workflow;

/// <summary>Yalnız geri ödəniş workflow-unun dəyişməyən əməliyyat identifikatorlarını saxlayır.</summary>
public static class RefundActionCode
{
    public const string SubmitPayoutDetails = "submitPayoutDetails";
    public const string ViewPayoutDetails = "viewPayoutDetails";
    public const string SubmitTransfer = "submitTransfer";
    public const string ConfirmTransfer = "confirmTransfer";
    public const string DisputeTransfer = "disputeTransfer";
}
