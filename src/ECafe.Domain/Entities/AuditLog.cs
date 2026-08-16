using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class AuditLog : AuditableSoftDeletableEntity<long>
    {
        public Guid? EventId { get; set; }

        public int? RestaurantId { get; set; }

        public int? UserId { get; set; }

        public string? ActorFullName { get; set; }

        public int? ActorRoleId { get; set; }

        public string? ActorRoleName { get; set; }

        public string? ActorEmail { get; set; }

        public string EntityName { get; set; } = null!;

        public long EntityId { get; set; }

        public string? EntityDisplayName { get; set; }

        public string Action { get; set; } = null!;

        public string? OldValues { get; set; }

        public string? NewValues { get; set; }

        public string? Metadata { get; set; }

        public string? CorrelationId { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime? OccurredAt { get; set; }

        public virtual User? User { get; set; }
    }
}
