namespace ECafe.Domain.Constants;

public static class DashboardFilterCodes
{
    public static class PaymentMode
    {
        public const string Real = "real";
        public const string Test = "test";
        public const string NotApplicable = "not_applicable";
    }

    public static class Period
    {
        public const string Today = "today";
        public const string Week = "week";
        public const string Month = "month";
        public const string Custom = "custom";
    }

    public static class PaymentEvent
    {
        public const string All = "all";
        public const string Receipt = "receipt";
        public const string Refund = "refund";
    }
}
