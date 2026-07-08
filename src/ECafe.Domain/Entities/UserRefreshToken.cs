using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class UserRefreshToken : AuditableSoftDeletableEntity<int>
    {
        public int UserId { get; set; }

        public string TokenHash { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }

        public DateTime? RevokedAt { get; set; }

        public string? ReplacedByTokenHash { get; set; }

        public string? CreatedByIp { get; set; }

        public string? RevokedByIp { get; set; }

        public string? UserAgent { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
