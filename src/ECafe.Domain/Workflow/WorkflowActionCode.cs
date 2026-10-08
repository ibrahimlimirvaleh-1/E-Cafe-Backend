namespace ECafe.Domain.Workflow;

// Immutable action identifiers shared by API handlers and workflow records.
public static class WorkflowActionCode
{
    public static class Contract
    {
        public const string SendForSignature = "sendForSignature";
        public const string Approve = "approve";
        public const string Activate = "activate";
        public const string Terminate = "terminate";
    }

    public static class Reservation
    {
        public const string SubmitPaymentProof = "submitPaymentProof";
        public const string SendPaymentInstruction = "sendPaymentInstruction";
        public const string Complete = "complete";
        public const string ApprovePaymentProof = "approvePaymentProof";
        public const string RejectPaymentProof = "rejectPaymentProof";
        public const string CheckIn = "checkIn";
        public const string MarkArrived = "markArrived";
        public const string Cancel = "cancel";
        public const string RequestRefund = "requestRefund";
        public const string WaiveDeposit = "waiveDeposit";
    }

    public static class Refund
    {
        public const string SubmitPayoutDetails = "submitPayoutDetails";
        public const string ViewPayoutDetails = "viewPayoutDetails";
        public const string SubmitTransfer = "submitTransfer";
        public const string ConfirmTransfer = "confirmTransfer";
        public const string DisputeTransfer = "disputeTransfer";
    }

    public static class Order
    {
        public const string SendToKitchen = "sendToKitchen";
        public const string Cancel = "cancel";
        public const string Serve = "serve";
        public const string Close = "close";
    }

    public static class Kitchen
    {
        public const string Accept = "accept";
        public const string StartPreparing = "startPreparing";
        public const string MarkReady = "markReady";
    }

    public static class Payment
    {
        public const string Pay = "pay";
        public const string MarkPaid = "markPaid";
        public const string Cancel = "cancel";
        public const string Retry = "retry";
        public const string Refund = "refund";
    }

}
