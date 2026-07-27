namespace ECafe.Application.Common.Outbox
{
    public sealed class EmailNotificationOutboxPayload
    {
        public string ToEmail { get; set; } = null!;
        public string ToName { get; set; } = null!;
        public string Subject { get; set; } = null!;
        public string Body { get; set; } = null!;
        public string? RelatedEntityType { get; set; }
        public long? RelatedEntityId { get; set; }
    }
}
