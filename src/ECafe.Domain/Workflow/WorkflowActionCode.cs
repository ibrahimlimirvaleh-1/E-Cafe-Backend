namespace ECafe.Domain.Workflow;

// Immutable action identifiers shared by API handlers and workflow records.
public static class WorkflowActionCode
{
    public static class Reservation
    {
        public const string ApprovePaymentProof = "approvePaymentProof";
        public const string RejectPaymentProof = "rejectPaymentProof";
        public const string CheckIn = "checkIn";
        public const string Cancel = "cancel";
        public const string RequestRefund = "requestRefund";
    }

    public static class Refund
    {
        public const string SubmitPayoutDetails = "submitPayoutDetails";
        public const string SubmitTransfer = "submitTransfer";
        public const string ConfirmTransfer = "confirmTransfer";
        public const string DisputeTransfer = "disputeTransfer";
    }

}
