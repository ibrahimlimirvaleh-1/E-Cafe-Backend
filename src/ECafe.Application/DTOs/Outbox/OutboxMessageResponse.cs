namespace ECafe.Application.DTOs.Outbox
{
    public class OutboxMessageResponse
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = null!;
        public string AggregateType { get; set; } = null!;
        public long AggregateId { get; set; }
        public int ChannelId { get; set; }
        public string Channel { get; set; } = null!;
        public int StatusId { get; set; }
        public string Status { get; set; } = null!;
        public string Recipient { get; set; } = null!;
        public string RecipientName { get; set; } = null!;
        public string Subject { get; set; } = null!;
        public int RetryCount { get; set; }
        public int MaxRetryCount { get; set; }
        public DateTime OccurredAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime? LockedUntil { get; set; }
        public DateTime? NextRetryAt { get; set; }
        public string? LastError { get; set; }
        public string? RelatedEntityType { get; set; }
        public long? RelatedEntityId { get; set; }
    }
}
