using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class OutboxEvent : BaseEntity<Guid>
    {
        public string EventType { get; set; } = null!;

        public string AggregateType { get; set; } = null!;

        public long AggregateId { get; set; }

        public string Payload { get; set; } = null!;

        public DateTime OccurredAt { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public DateTime? LockedUntil { get; set; }

        public int RetryCount { get; set; }

        public string? LastError { get; set; }
    }
}
