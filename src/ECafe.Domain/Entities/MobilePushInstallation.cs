namespace ECafe.Domain.Entities;

public sealed class MobilePushInstallation
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public string SessionId { get; set; } = null!;
    public Guid ExpoProjectId { get; set; }
    public string TokenHash { get; set; } = null!;
    public string ProtectedToken { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
}
