using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class PasswordResetToken : AuditableSoftDeletableEntity<int>
{
    public int UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? CreatedByIp { get; set; }

    public string? UserAgent { get; set; }

    public virtual User User { get; set; } = null!;

    public bool IsUsed => UsedAt.HasValue;

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpired(DateTime nowUtc) => ExpiresAt <= nowUtc;

    public bool IsActive(DateTime nowUtc) => !IsUsed && !IsRevoked && !IsExpired(nowUtc) && !IsDeleted;
}
