namespace ECafe.Application.DTOs.AuditLog
{
    public class AuditLogResponse
    {
        public long Id { get; set; }

        public Guid? EventId { get; set; }

        public int? RestaurantId { get; set; }

        public int? UserId { get; set; }

        public string EntityName { get; set; } = null!;

        public long EntityId { get; set; }

        public string? EntityDisplayName { get; set; }

        public string Action { get; set; } = null!;

        public string? NewValues { get; set; }

        public string? Metadata { get; set; }

        public string? CorrelationId { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime? OccurredAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
