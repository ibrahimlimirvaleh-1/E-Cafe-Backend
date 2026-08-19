namespace ECafe.Application.Common.Outbox
{
    public sealed class SmsNotificationOutboxPayload
    {
        public string ToPhone { get; set; } = null!;
        public string ToName { get; set; } = null!;
        public string Subject { get; set; } = null!;
        public string Body { get; set; } = null!;
        public string? RelatedEntityType { get; set; }
        public long? RelatedEntityId { get; set; }
    }
}
