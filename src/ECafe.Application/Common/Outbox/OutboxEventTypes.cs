namespace ECafe.Application.Common.Outbox
{
    public static class OutboxEventTypes
    {
        public const string AuditLogRequested = "AuditLogRequested";
        public const string EmailNotificationRequested = "EmailNotificationRequested";
        public const string SmsNotificationRequested = "SmsNotificationRequested";
    }
}
