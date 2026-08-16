namespace ECafe.Application.DTOs.AuditLog
{
    public class AuditLogResponse
    {
        public long Id { get; set; }

        public Guid? EventId { get; set; }

        public int? RestaurantId { get; set; }

        public int? UserId { get; set; }

        public int? ActorUserId { get; set; }

        public string? ActorFullName { get; set; }

        public string? UserName { get; set; }

        public int? ActorRoleId { get; set; }

        public string? ActorRoleName { get; set; }

        public string? RoleName { get; set; }

        public string? ActorEmail { get; set; }

        public string EntityName { get; set; } = null!;

        public long EntityId { get; set; }

        public string? EntityDisplayName { get; set; }

        public string Action { get; set; } = null!;

        public string ActionDisplayName { get; set; } = null!;

        public string? NewValues { get; set; }

        public string? Metadata { get; set; }

        public string? CorrelationId { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime? OccurredAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
