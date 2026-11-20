using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public sealed class EmailChangeRequest : AuditableSoftDeletableEntity<int>
{
    public int UserId { get; set; }
    public string OriginalEmail { get; set; } = null!;
    public string NewEmail { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public int SessionVersion { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public User User { get; set; } = null!;
}
