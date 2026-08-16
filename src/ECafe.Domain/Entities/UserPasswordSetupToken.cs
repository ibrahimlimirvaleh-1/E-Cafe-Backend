using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class UserPasswordSetupToken : AuditableSoftDeletableEntity<int>
    {
        public int UserId { get; set; }
        public string TokenHash { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }

        public User User { get; set; } = null!;

        public bool IsUsed => UsedAt.HasValue;
        public bool IsExpired(DateTime nowUtc) => ExpiresAt <= nowUtc;
        public bool IsActive(DateTime nowUtc) => !IsUsed && !IsExpired(nowUtc) && !IsDeleted;
    }
}
