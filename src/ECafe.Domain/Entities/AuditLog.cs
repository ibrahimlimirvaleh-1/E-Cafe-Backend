using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class AuditLog : AuditableSoftDeletableEntity<long>
    {
        public int? UserId { get; set; }

        public string EntityName { get; set; } = null!;

        public long EntityId { get; set; }

        public string Action { get; set; } = null!;

        public string? OldValues { get; set; }

        public string? NewValues { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public virtual User? User { get; set; }
    }
}
