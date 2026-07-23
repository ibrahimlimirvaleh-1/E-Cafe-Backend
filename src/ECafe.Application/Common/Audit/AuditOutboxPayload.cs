namespace ECafe.Application.Common.Audit
{
    public class AuditOutboxPayload
    {
        public int RestaurantId { get; set; }

        public int? ActorUserId { get; set; }

        public string EntityType { get; set; } = null!;

        public long EntityId { get; set; }

        public string? EntityDisplayName { get; set; }

        public string Action { get; set; } = null!;

        public string? Metadata { get; set; }

        public string? CorrelationId { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime OccurredAt { get; set; }
    }
}
