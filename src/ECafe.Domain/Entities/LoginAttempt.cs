using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class LoginAttempt : AuditableSoftDeletableEntity<int>
{
    public int? UserId { get; set; }
    public string Email { get; set; } = null!;
    public bool IsSuccessful { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime AttemptedAt { get; set; }
    public DateTime? LockoutUntil { get; set; }

    public User? User { get; set; }
}
