namespace ECafe.Domain.Workflow;

/// <summary>Yalnız rezervasiya workflow-unun dəyişməyən əməliyyat identifikatorlarını saxlayır.</summary>
public static class ReservationActionCode
{
    public const string SendPaymentInstruction = "sendPaymentInstruction";
    public const string SubmitPaymentProof = "submitPaymentProof";
    public const string ApprovePaymentProof = "approvePaymentProof";
    public const string RejectPaymentProof = "rejectPaymentProof";
    public const string CheckIn = "checkIn";
    public const string MarkArrived = "markArrived";
    public const string Cancel = "cancel";
    public const string Complete = "complete";
    public const string RequestRefund = "requestRefund";
    public const string WaiveDeposit = "waiveDeposit";
}
